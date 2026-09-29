cbuffer Parameters : register(b0) {
    float4 Size;       // desktop width/height, detection width/height
    float4 Geometry;   // source pixels per detection pixel, minimum run, minimum y
    float4 BirdRect;   // source-relative left/top, width/height of sprite in source pixels
    float4 BirdInfo;   // logical width/height, scale, facing
    float4 CursorRect;
    float4 Excluded[8];
    float4 Tracking;   // reference center x/y, patch width/height
    float4 Prediction; // expected center x/y, search radius, unused
    float4 CaptureInfo; // raw texture width/height and DXGI rotation
};
Texture2D<float4> Desktop : register(t0);
Texture2D<float4> InputImage : register(t1);
Texture2D<float4> Bird : register(t2);
RWTexture2D<float4> ImageOut : register(u0);
RWStructuredBuffer<float4> Lines : register(u1);
float4 desktopPixel(int2 p) {
    if(CaptureInfo.z==2) p=int2(p.y,(int)CaptureInfo.y-1-p.x);
    else if(CaptureInfo.z==3) p=(int2)CaptureInfo.xy-1-p;
    else if(CaptureInfo.z==4) p=int2((int)CaptureInfo.x-1-p.y,p.x);
    return Desktop.Load(int3(p,0));
}

bool inRect(float2 p, float4 r) { return all(p >= r.xy) && all(p < r.xy+r.zw); }
bool valid(float2 p) {
    if (any(p < 0) || any(p >= Size.xy) || inRect(p, CursorRect)) return false;
    [unroll] for(int i=0;i<8;i++) if(inRect(p,Excluded[i])) return false;
    if(inRect(p,BirdRect) && BirdInfo.z > 0) {
        int2 q = (p-BirdRect.xy)/BirdInfo.z;
        if(BirdInfo.w < 0) q.x = (int)BirdInfo.x-1-q.x;
        [unroll] for(int yy=-1;yy<=1;yy++) [unroll] for(int xx=-1;xx<=1;xx++)
            if(Bird.Load(int3(q+int2(xx,yy),0)).a > 0) return false;
    }
    return true;
}
// BOX resample: bounded four-tap coverage of each downsampled pixel.
[numthreads(8,8,1)]
void Downsample(uint3 id : SV_DispatchThreadID) {
    if(any(id.xy >= (uint2)Size.zw)) return;
    float2 p = (id.xy+.5)*Geometry.xy;
    float4 total=0;
    [unroll] for(int yy=-1;yy<=1;yy+=2) [unroll] for(int xx=-1;xx<=1;xx+=2) {
        float2 q=p+float2(xx,yy)*Geometry.xy*.25;
        if(valid(q)) total += float4(desktopPixel((int2)q).rgb,1);
    }
    ImageOut[id.xy] = total.a >= 4 ? float4(total.rgb/total.a,1) : 0;
}
[numthreads(8,8,1)]
void HorizontalBox(uint3 id : SV_DispatchThreadID) {
    if(any(id.xy >= (uint2)Size.zw)) return;
    float4 total=0;
    [unroll] for(int dx=-2;dx<=2;dx++) total += InputImage.Load(int3((int2)id.xy+int2(dx,0),0));
    ImageOut[id.xy] = total.a >= 5 ? float4(total.rgb/5,1) : 0;
}
[numthreads(8,8,1)]
void Edges(uint3 id : SV_DispatchThreadID) {
    if(any(id.xy >= (uint2)Size.zw)) return;
    int2 p=id.xy;
    float4 above=0,below=0,deep=0;
    [unroll] for(int dy=1;dy<=3;dy++) {
        above+=InputImage.Load(int3(p-int2(0,dy),0));
        below+=InputImage.Load(int3(p+int2(0,dy-1),0));
        deep+=InputImage.Load(int3(p+int2(0,dy+3),0));
    }
    float contrast=length(above.rgb-below.rgb)/3;
    float thickness=length(below.rgb-deep.rgb)/3;
    float3 a1=InputImage.Load(int3(p-int2(0,1),0)).rgb;
    float aboveVariation=max(length(a1-InputImage.Load(int3(p-int2(0,2),0)).rgb),
        length(a1-InputImage.Load(int3(p-int2(0,3),0)).rgb));
    float confidence = contrast >= .07 && thickness < max(.075,contrast*.7) &&
        aboveVariation < max(.08,contrast*.7) ? saturate(contrast*3) : 0;
    float polarity=dot(below.rgb-above.rgb,float3(.299,.587,.114))>=0?1:-1;
    ImageOut[id.xy] = float4(confidence,polarity,0,above.a+below.a+deep.a>=9 ? 1 : 0);
}
// Eight longest runs per row bound both GPU output and CPU readback size.
[numthreads(32,1,1)]
void ExtractRows(uint3 id : SV_DispatchThreadID) {
    uint y=id.x;
    if(y >= (uint)Size.w) return;
    float4 best[8];
    [unroll] for(int k=0;k<8;k++) best[k]=0;
    int start=-1,last=-1,gaps=0,unknown=0,unknownRun=0; float score=0,polarity=0; int hits=0;
    for(int x=0;x<=(int)Size.z;x++) {
        float4 e=InputImage.Load(int3(x,y,0));
        bool on=x<(int)Size.z && e.a>.5 && e.r>.12 && y>=Geometry.w;
        bool masked=x<(int)Size.z && e.a<.5;
        if(on) { if(start<0) start=x; last=x; gaps=0;unknownRun=0;score+=e.r;polarity+=e.g; hits++; }
        else if(masked && start>=0) { unknown++;unknownRun++; }
        else { gaps++;unknownRun=0; }
        // A cursor or the perched bird hides evidence; it does not cut the platform
        // in two. Bridge only bounded unknown spans with real edges on both sides.
        // Unknown pixels never contribute to the edge score or observed coverage.
        if(start>=0 && (gaps>2 || unknownRun>max(64/Geometry.x,Geometry.z*.65) || x==(int)Size.z)) {
            int length=last-start+1;
            int hidden=max(0,unknown-unknownRun);
            if(length>=Geometry.z && hits>=length*.6 && hits>=(length-hidden)*.85) {
                float4 segment=float4(start,last+1,y,(polarity>=0?1:-1)*score/max(hits,1));
                [unroll] for(int k=0;k<8;k++) if(segment.y-segment.x>best[k].y-best[k].x) {
                    float4 old=best[k];best[k]=segment;segment=old;
                }
            }
            start=-1;score=0;polarity=0;hits=0;unknown=0;unknownRun=0;gaps=0;
        }
    }
    [loop] for(int k=0;k<8;k++) {
        float4 segment=best[k];
        if(segment.y>segment.x) {
            // Refine Y against original pixels, so downsampling cannot float the feet.
            float strongest=0;int bestY=(int)round(segment.z*Geometry.y),bestSupport=0,bestValid=0;
            int center=bestY;
            [loop] for(int yy=center-(int)ceil(Geometry.y*3);yy<=center+(int)ceil(Geometry.y*3);yy++) {
                float contrast=0;int validCount=0,support=0;
                [loop] for(int sample=1;sample<=17;sample++) {
                    int xx=(int)lerp(segment.x,segment.y,sample/18.0)*Geometry.x;
                    if(valid(float2(xx,yy-1))&&valid(float2(xx,yy))) {
                        float strength=length(desktopPixel(int2(xx,yy)).rgb-desktopPixel(int2(xx,yy-1)).rgb);
                        contrast+=strength;validCount++;if(strength>.07) support++;
                    }
                }
                if(validCount>=10 && contrast/validCount>strongest) { strongest=contrast/validCount;bestY=yy;bestSupport=support;bestValid=validCount; }
            }
            segment.z=bestY/Geometry.y;
            if(bestValid<10 || bestSupport<ceil(bestValid*.88)) segment=0;
        }
        Lines[y*8+k]=segment;
    }
}
