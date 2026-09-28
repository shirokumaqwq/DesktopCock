using DesktopCock.Core;

var baseline = new Input(new DateTime(2026,9,28,12,0,0),0,false,false,false,false,false,0,1);
int passed = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); }
void Run(string title, Action test) { test(); Console.WriteLine($"PASS {title}"); passed++; }
void Advance(Behavior b, double seconds, Input input) { for (int i=0;i<(int)Math.Ceiling(seconds/.05);i++) b.Tick(.05,input); }

Run("Day/night idle thresholds and crossing midnight", () => {
    var b = new Behavior(new Settings(),1);
    b.Tick(.05,baseline with { IdleSeconds=599 }); Check(b.State != Mood.Sleep,"Day sleep too early");
    b.Tick(.05,baseline with { IdleSeconds=600 }); Check(b.State == Mood.Sleep,"Day sleep missing");
    b.Resume(); b.Tick(.05,baseline with { LocalTime=baseline.LocalTime.AddHours(11),IdleSeconds=119 }); Check(b.State != Mood.Sleep,"Night sleep too early");
    b.Tick(.05,baseline with { LocalTime=baseline.LocalTime.AddHours(11),IdleSeconds=120 }); Check(b.State == Mood.Sleep,"Night sleep missing");
    b.Tick(.05,baseline with { LocalTime=baseline.LocalTime.AddHours(12),IdleSeconds=121 }); Check(b.State == Mood.Sleep,"Midnight must remain asleep");
});
Run("Hover cannot wake; keyboard activity wakes without irritation", () => {
    var b = new Behavior(new Settings(),2);
    b.Tick(.05,baseline with { IdleSeconds=600 });
    b.Tick(.05,baseline with { IdleSeconds=601,Near=true,AtFeet=true }); Check(b.State==Mood.Sleep,"Hover woke bird");
    b.Tick(.05,baseline); Check(b.State==Mood.Stretch && b.Annoyance==0,"Input did not wake peacefully");
    Advance(b,1.1,baseline); Check(b.State != Mood.Stretch && b.State != Mood.Bite,"Stretch stuck");
});
Run("Wake-click stretches before biting; bite has finite duration and cooldown", () => {
    var b = new Behavior(new Settings(),3); b.Tick(.05,baseline with { IdleSeconds=600 });
    b.Tick(.05,baseline with { Near=true,Clicked=true }); Check(b.State==Mood.Stretch,"Must stretch before bite");
    Advance(b,.9,baseline with { Near=true }); Check(b.State==Mood.Stretch,"Stretch interrupted");
    Advance(b,.15,baseline with { Near=true }); Check(b.State==Mood.Bite,"Wake irritation lost before bite");
    Advance(b,.5,baseline with { Near=true }); Check(b.State!=Mood.Bite && b.Annoyance<=20,"Bite stuck");
    for(int i=0;i<5;i++) b.Tick(.05,baseline with {Near=true,Clicked=true});
    Check(b.State!=Mood.Bite,"Cooldown ignored");
});
Run("Mouse leaving before wake stretch finishes prevents chasing", () => {
    var b = new Behavior(new Settings(),4); b.Tick(.05,baseline with { IdleSeconds=600 });
    b.Tick(.05,baseline with { Near=true,Clicked=true }); Advance(b,1.2,baseline);
    Check(b.State != Mood.Bite,"Bird chased remote cursor");
});
Run("Gentle motion required for petting; release and leave end petting", () => {
    var b = new Behavior(new Settings(),5);
    var pet = baseline with { Near=true,AtHead=true,Held=true,Speed=12 };
    Advance(b,.2,pet); Check(b.State!=Mood.Pet,"Pet confirmed prematurely");
    Advance(b,.2,pet); Check(b.State==Mood.Pet,"Gentle motion not recognized");
    b.Tick(.05,pet with { Held=false }); Check(b.State!=Mood.Pet,"Release did not stop petting");
    Advance(b,.4,pet); b.Tick(.05,pet with { AtHead=false }); Check(b.State!=Mood.Pet,"Leaving head did not stop petting");
    Advance(b,.5,pet with { Speed=0 }); Check(b.State!=Mood.Pet,"Static hold counted as petting");
});
Run("Rough or prolonged petting causes irritation and bite", () => {
    var b = new Behavior(new Settings(),6); bool bit = false;
    for(int i=0;i<100;i++) { b.Tick(.05,baseline with {Near=true,AtHead=true,Held=true,Speed=70}); bit |= b.State==Mood.Bite; }
    Check(bit,"Rough gesture never bites");
    b = new Behavior(new Settings(),6); bit=false;
    for(int i=0;i<300;i++) { b.Tick(.05,baseline with {Near=true,AtHead=true,Held=true,Speed=10}); bit |= b.State==Mood.Bite; }
    Check(bit,"Prolonged petting never bites");
});
Run("Repeated clicks bite and feet hover takes priority over looking", () => {
    var b = new Behavior(new Settings(),7);
    b.Tick(.05,baseline with {Near=true,AtFeet=true}); Check(b.State==Mood.Lift,"Foot response missing");
    for(int i=0;i<4;i++) b.Tick(.05,baseline with {Near=true,Clicked=true,CursorDirection=-1});
    Check(b.State==Mood.Bite && b.Direction==-1,"Repeated-click directional bite missing");
});
Run("Song eligibility, interruption, and cooldown", () => {
    var s = new Settings { ActivityMinSeconds=1000,ActivityMaxSeconds=1000,SingChance=1 };
    var b = new Behavior(s,8);
    Advance(b,59,baseline); Check(b.State!=Mood.Sing,"Sang before eligible");
    bool sang=false;
    for(int i=0;i<50;i++) { b.Tick(.05,baseline); sang |= b.State==Mood.Sing; }
    Check(sang,"Eligible song missing");
    b.Tick(.05,baseline with {Near=true}); Check(b.State==Mood.Look,"Song cannot be interrupted");
    for(int i=0;i<1200;i++) { b.Tick(.05,baseline); Check(b.State!=Mood.Sing,"Song cooldown ignored"); }
});
Run("Seeded decisions deterministic; edge reverses movement", () => {
    var a=new Behavior(new Settings(),42); var b=new Behavior(new Settings(),42);
    for(int i=0;i<3000;i++) { a.Tick(.05,baseline); b.Tick(.05,baseline); Check(a.State==b.State && a.Direction==b.Direction,"Nondeterministic behavior"); }
    a.Force(Mood.Walk); a.Tick(.05,baseline); double movement=a.Movement;
    a.TurnAtEdge(); a.Tick(.05,baseline); Check(a.Movement == -movement,"Edge did not reverse walk");
});
Run("Suspend does not replay elapsed animation or activity", () => {
    var b=new Behavior(new Settings(),9); b.Tick(3600,baseline);
    Check(b.StateAge<=.1 && b.State==Mood.Idle,"Suspended time replayed");
});
Run("Invalid settings rejected and scale clamped", () => {
    var s=new Settings { Scale=100 }; s.Validate(); Check(s.Scale==4,"Scale not clamped");
    bool rejected=false; try { new Settings { PetMaxSpeed=-1 }.Validate(); } catch(InvalidDataException) { rejected=true; }
    Check(rejected,"Negative speed accepted");
});
Run("Thirty simulated minutes: all states reachable, no conflicting movement or unbounded irritation", () => {
    var settings=new Settings { SingChance=1 };
    var b=new Behavior(settings,15); var seen=new HashSet<Mood>();
    for(int i=0;i<36000;i++)
    {
        double t=i*.05; double cycle=t%180;
        var input=baseline with { LocalTime=baseline.LocalTime.AddSeconds(t) };
        if(cycle is >=100 and <102) input=input with {Near=true,AtFeet=true};
        if(cycle is >=105 and <106) input=input with {Near=true};
        if(cycle is >=110 and <125) input=input with {Near=true,AtHead=true,Held=true,Speed=12};
        if(cycle is >=140 and <150) input=input with {IdleSeconds=601};
        b.Tick(.05,input); seen.Add(b.State);
        Check(b.Annoyance is >=0 and <=100,"Unbounded irritation");
        Check(b.State==Mood.Walk || b.Movement==0,"Moved while not walking");
    }
    foreach(var state in Enum.GetValues<Mood>()) Check(seen.Contains(state),$"Unreachable state: {state}");
});
Run("Preview replay resets animation and resume clears held gesture", () => {
    var b=new Behavior(new Settings(),16); b.Force(Mood.Stretch); Advance(b,2,baseline);
    b.Force(Mood.Stretch); Check(b.StateAge==0,"Preview did not restart"); b.Resume();
    Check(b.State==Mood.Idle,"Preview did not exit");
});
Run("Repeated clicks during wake animation cannot exceed irritation bounds", () => {
    var b=new Behavior(new Settings(),17); b.Tick(.05,baseline with { IdleSeconds=600 });
    for(int i=0;i<15;i++) b.Tick(.05,baseline with { Near=true,Clicked=true });
    Check(b.State==Mood.Stretch && b.Annoyance==100,"Wake animation irritation overflow");
});
Console.WriteLine($"{passed} behavior scenarios passed.");
