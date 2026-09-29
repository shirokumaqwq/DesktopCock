cbuffer Parameters : register(b0) {
    float4 Size; float4 Geometry; float4 BirdRect; float4 BirdInfo; float4 CursorRect;
    float4 Excluded[8]; float4 Tracking; float4 Prediction;
    float4 CaptureInfo;
};
Texture2D<float4> Desktop : register(t0);
Texture2D<float4> Reference : register(t1);
Texture2D<float4> Bird : register(t2);
RWTexture2D<float4> ReferenceOut : register(u0);
RWStructuredBuffer<float4> Result : register(u1);
float4 desktopPixel(int2 p) {
    if(CaptureInfo.z==2) p=int2(p.y,(int)CaptureInfo.y-1-p.x);
    else if(CaptureInfo.z==3) p=(int2)CaptureInfo.xy-1-p;
    else if(CaptureInfo.z==4) p=int2((int)CaptureInfo.x-1-p.y,p.x);
    return Desktop.Load(int3(p,0));
}
bool inRect(float2 p,float4 r) {return all(p>=r.xy)&&all(p<r.xy+r.zw);}
bool valid(float2 p) {
    if(any(p<0)||any(p>=Size.xy)||inRect(p,CursorRect)) return false;
    [unroll] for(int i=0;i<8;i++) if(inRect(p,Excluded[i])) return false;
    if(inRect(p,BirdRect)&&BirdInfo.z>0) {
        int2 q=(p-BirdRect.xy)/BirdInfo.z;
        if(BirdInfo.w<0) q.x=(int)BirdInfo.x-1-q.x;
        if(Bird.Load(int3(q,0)).a>0) return false;
    }
    return true;
}
float2 patch(uint2 p) { return ((float2(p)+.5)/float2(32,16)-.5)*Tracking.zw; }
[numthreads(8,8,1)]
void SaveReference(uint3 id:SV_DispatchThreadID) {
    if(id.x>=32||id.y>=16) return;
    float2 p=Tracking.xy+patch(id.xy);
    ReferenceOut[id.xy]=valid(p)?float4(desktopPixel((int2)p).rgb,1):0;
}
float cost(float2 center) {
    float total=0;int count=0;
    [loop] for(int y=0;y<16;y+=2) [loop] for(int x=0;x<32;x+=2) {
        float4 a=Reference.Load(int3(x,y,0)); float2 p=center+patch(uint2(x,y));
        if(a.a<.5||!valid(p)) continue;
        float3 d=abs(a.rgb-desktopPixel((int2)p).rgb);
        total+=(d.r+d.g+d.b)/3; count++;
    }
    return count>=48?total/count:10;
}
groupshared float costs[1089];
groupshared float2 coarse;
groupshared float coarseSecond;
[numthreads(256,1,1)]
void Match(uint tid:SV_GroupIndex) {
    float step=max(1,Prediction.z/16);
    for(uint i=tid;i<1089;i+=256) {
        float2 d=float2((int)(i%33)-16,(int)(i/33)-16)*step;
        costs[i]=cost(Prediction.xy+d)+length(d)*.000002;
    }
    GroupMemoryBarrierWithGroupSync();
    if(tid==0) {
        uint best=0;for(uint j=1;j<1089;j++) if(costs[j]<costs[best]) best=j;
        coarse=float2((int)(best%33)-16,(int)(best/33)-16)*step;
        // Keep competing vertical matches from the entire search. Looking only
        // near the winner would miss an identical card farther away in the ROI.
        coarseSecond=10;
        for(uint j=0;j<1089;j++)
            if(abs((int)(j/33)-(int)(best/33))>2) coarseSecond=min(coarseSecond,costs[j]);
    }
    GroupMemoryBarrierWithGroupSync();
    for(uint i=tid;i<289;i+=256) {
        float2 d=coarse+float2((int)(i%17)-8,(int)(i/17)-8);
        costs[i]=cost(Prediction.xy+d)+length(d)*.000002;
    }
    GroupMemoryBarrierWithGroupSync();
    if(tid==0) {
        uint best=0;for(uint j=1;j<289;j++) if(costs[j]<costs[best]) best=j;
        float2 d=coarse+float2((int)(best%17)-8,(int)(best/17)-8);
        float second=coarseSecond;
        for(uint j=0;j<289;j++)
            if(abs((int)(j/17)-(int)(best/17))>4) second=min(second,costs[j]);
        float2 center=Prediction.xy+d;
        // Flat patches can tie across a few Y offsets. Snap only to a verified
        // native-resolution edge, rather than accumulating those ties as drift.
        float strongest=.08;float edgeY=center.y;
        [loop] for(int yy=(int)round(center.y)-4;yy<=(int)round(center.y)+4;yy++) {
            float contrast=0;int count=0;
            [loop] for(int xx=1;xx<=7;xx++) {
                float px=center.x+(xx/8.0-.5)*Tracking.z;
                if(valid(float2(px,yy))&&valid(float2(px,yy-1))) {
                    contrast+=length(desktopPixel(int2(px,yy)).rgb-desktopPixel(int2(px,yy-1)).rgb);count++;
                }
            }
            if(count>=4 && contrast/count>strongest) { strongest=contrast/count;edgeY=yy; }
        }
        Result[0]=float4(center.x,edgeY,costs[best],second-costs[best]);
    }
}
