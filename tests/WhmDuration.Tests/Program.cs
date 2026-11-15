using WhmDuration.Core;

var tests = new List<(string Name, Action Run)>();
void Test(string name, Action run) => tests.Add((name, run));
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"expected {expected}, got {actual}"); }
void True(bool value) { if (!value) throw new Exception("expected true"); }
var dia = new EffectDefinition(16532, 1871, 30, EffectTarget.Enemy);

// Breaks caught: wrong action/status mapping, source filtering, timer clamp, target branch, periodic arithmetic, unsupported input parsing.
foreach (var (action, status, duration, mode, kind) in new (uint, uint, float, EffectTarget, TimerKind)[] {
    (121,143,30,EffectTarget.Enemy, TimerKind.TargetStatus), (132,144,30,EffectTarget.Enemy, TimerKind.TargetStatus), (16532,1871,30,EffectTarget.Enemy, TimerKind.TargetStatus),
    (137,158,18,EffectTarget.FriendlyOrSelf, TimerKind.TargetStatus), (133,150,15,EffectTarget.Self, TimerKind.SelfStatus), (37010,3880,15,EffectTarget.Self, TimerKind.SelfStatus) })
    Test($"action {action} tracks the correct effect", () => Equal(new EffectDefinition(action,status,duration,mode,kind), DurationModel.Find(action)));
Test("unknown actions are untouched", () => Equal<EffectDefinition?>(null, DurationModel.Find(119)));
Test("owned status duration is read", () => Equal(18f, DurationModel.SelectRemaining(dia, 42, [new(1871,42,18)])));
Test("another player's status is ignored", () => Equal(0f, DurationModel.SelectRemaining(dia,42,[new(1871,7,18)])));
Test("different status is ignored", () => Equal(0f, DurationModel.SelectRemaining(dia,42,[new(143,42,18)])));
Test("unloaded player cannot own statuses", () => Equal(0f, DurationModel.SelectRemaining(dia,0,[new(1871,0,18)])));
Test("invalid source sentinel is ignored", () => Equal(0f, DurationModel.SelectRemaining(dia,0xE0000000,[new(1871,0xE0000000,18)])));
Test("expired status is ignored", () => Equal(0f, DurationModel.SelectRemaining(dia,42,[new(1871,42,0),new(1871,42,-1)])));
Test("nonfinite statuses are ignored", () => Equal(0f, DurationModel.SelectRemaining(dia,42,[new(1871,42,float.NaN),new(1871,42,float.PositiveInfinity)])));
Test("refresh is read without stale timer", () => { Equal(2f,DurationModel.SelectRemaining(dia,42,[new(1871,42,2)])); Equal(30f,DurationModel.SelectRemaining(dia,42,[new(1871,42,30)])); });
Test("dispel clears the timer", () => Equal(0f,DurationModel.SelectRemaining(dia,42,[])));
Test("excess remaining time clamps to duration", () => Equal(30f,DurationModel.SelectRemaining(dia,42,[new(1871,42,31)])));
Test("cooldown sweep uses elapsed fraction", () => Equal<CooldownVisual?>(new(40,18),DurationModel.GetVisual(18,30,false)));
Test("fresh status starts the sweep", () => Equal<CooldownVisual?>(new(0,30),DurationModel.GetVisual(30,30,false)));
Test("seconds round up", () => Equal<CooldownVisual?>(new(96,1),DurationModel.GetVisual(0.1f,3,false)));
Test("real cooldown takes priority", () => Equal<CooldownVisual?>(null,DurationModel.GetVisual(18,30,true)));
foreach (var remaining in new[]{0f,-1f,float.NaN,float.PositiveInfinity})
    Test($"invalid remaining {remaining} creates no cooldown", () => Equal<CooldownVisual?>(null,DurationModel.GetVisual(remaining,30,false)));
foreach (var duration in new[]{0f,-1f,float.NaN,float.PositiveInfinity})
    Test($"invalid duration {duration} creates no cooldown", () => Equal<CooldownVisual?>(null,DurationModel.GetVisual(10,duration,false)));
Test("too-long timer clamps the sweep", () => Equal<CooldownVisual?>(new(0,30),DurationModel.GetVisual(99,30,false)));
Test("DoT follows enemy", () => Equal(7u,DurationModel.SelectTarget(EffectTarget.Enemy,42,7,TargetKind.Hostile)));
Test("DoT ignores friendly target", () => Equal(0u,DurationModel.SelectTarget(EffectTarget.Enemy,42,7,TargetKind.Friendly)));
Test("DoT clears with no target", () => Equal(0u,DurationModel.SelectTarget(EffectTarget.Enemy,42,0,TargetKind.None)));
Test("Regen follows friendly", () => Equal(7u,DurationModel.SelectTarget(EffectTarget.FriendlyOrSelf,42,7,TargetKind.Friendly)));
Test("Regen falls back to self for enemy", () => Equal(42u,DurationModel.SelectTarget(EffectTarget.FriendlyOrSelf,42,7,TargetKind.Hostile)));
Test("Regen falls back to self with no target", () => Equal(42u,DurationModel.SelectTarget(EffectTarget.FriendlyOrSelf,42,0,TargetKind.None)));
Test("Medica uses self even with friendly target", () => Equal(42u,DurationModel.SelectTarget(EffectTarget.Self,42,7,TargetKind.Friendly)));

// GroundRecast tests
Test("GroundRecast early phase displays duration countdown", () => {
    var v = DurationModel.GetGroundVisual(6f, 24f, 90f);
    Equal<CooldownVisual?>(new(25, 18), v);
});
Test("GroundRecast at t=0 displays 0% and full duration", () => {
    var v = DurationModel.GetGroundVisual(0f, 24f, 90f);
    Equal<CooldownVisual?>(new(0, 24), v);
});
Test("GroundRecast expired phase yields to native cooldown", () => {
    var v = DurationModel.GetGroundVisual(24f, 24f, 90f);
    Equal<CooldownVisual?>(null, v);
});
Test("GroundRecast past duration yields to native cooldown", () => {
    var v = DurationModel.GetGroundVisual(50f, 24f, 90f);
    Equal<CooldownVisual?>(null, v);
});

// All-Job Catalog tests
foreach (var (act, kind) in new (uint, TimerKind)[] {
    (3569, TimerKind.GroundRecast), // Asylum
    (188, TimerKind.GroundRecast),  // Sacred Soil
    (3639, TimerKind.GroundRecast), // Salted Earth
    (25862, TimerKind.GroundRecast),// Liturgy of the Bell
    (16540, TimerKind.TargetStatus),// Biolysis
    (16554, TimerKind.TargetStatus),// Combust III
    (24314, TimerKind.TargetStatus),// Eukrasian Dosis III
    (7489, TimerKind.TargetStatus), // Higanbana
    (7407, TimerKind.TargetStatus), // Stormbite
    (25772, TimerKind.TargetStatus),// Chaotic Spring
    (16153, TimerKind.TargetStatus),// Sonic Break
    (23, TimerKind.TargetStatus),   // Circle of Scorn
    (36986, TimerKind.TargetStatus),// High Thunder
    (2270, TimerKind.SelfStatus),   // Doton
    (25823, TimerKind.SelfStatus),  // Slipstream
}) {
    Test($"catalog contains action {act} as {kind}", () => {
        var eff = DurationModel.Find(act);
        True(eff != null);
        Equal(kind, eff!.Kind);
    });
}


var descriptions = new (uint Action,string Text,int Initial,int Tick,int Ticks,int Total,bool Heal,bool Party)[] {
    (121,"Deals wind damage with a potency of 50.\nAdditional Effect: Wind damage over time\nPotency: 30\nDuration: 30s",50,30,10,350,false,false),
    (132,"Deals wind damage with a potency of 50.\nAdditional Effect: Wind damage over time\nPotency: 50\nDuration: 30s",50,50,10,550,false,false),
    (16532,"Deals unaspected damage with a potency of 85.\nAdditional Effect: Unaspected damage over time\nPotency: 85\nDuration: 30s",85,85,10,935,false,false),
    (137,"Grants healing over time effect to target.\nCure Potency: 250\nDuration: 18s",0,250,6,1500,true,false),
    (133,"Restores own HP and the HP of all nearby party members.\nCure Potency: 250\nAdditional Effect: Regen\nCure Potency: 150\nDuration: 15s",250,150,5,1000,true,true),
    (37010,"Restores own HP and the HP of all nearby party members.\nCure Potency: 250\nAdditional Effect: Regen\nCure Potency: 175\nDuration: 15s",250,175,5,1125,true,true) };
foreach (var example in descriptions) {
    Test($"action {example.Action} full-duration potency", () => {
        var parsed = PotencyTooltip.Parse(example.Action,example.Text) ?? throw new Exception("missing breakdown");
        Equal(example.Initial,parsed.Initial); Equal(example.Tick,parsed.Tick); Equal(example.Ticks,parsed.TickCount);
        Equal(example.Total,parsed.Total); Equal(example.Heal,parsed.Healing); Equal(example.Party,parsed.PerRecipient);
    });
    Test($"action {example.Action} preserves description and labels total", () => {
        var result = PotencyTooltip.Append(example.Action,example.Text);
        True(result.StartsWith(example.Text)); True(result.Contains(example.Total.ToString()));
        True(result.Contains("full duration")); True(result.Contains("3s"));
        if (example.Party) True(result.Contains("per recipient"));
        Equal(result,PotencyTooltip.Append(example.Action,result));
    });
}
Test("trait-adjusted values come from visible text", () => Equal(1100,PotencyTooltip.Parse(16532,"Deals unaspected damage with a potency of 100.\nAdditional Effect: Unaspected damage over time\nPotency: 100\nDuration: 30s")?.Total));
foreach (var text in new[]{"", "Inflicts damage.", "Potency: 85\nDuration: 30s", "Deals unaspected damage with a potency of 85.\nPotency: 85\nDuration: 31s", "Dégâts: 85", "Deals unaspected damage with a potency of 85.\nPotency: 2147483648\nDuration: 30s"})
    Test($"unknown tooltip form stays unchanged: {text}", () => Equal(text,PotencyTooltip.Append(16532,text)));
Test("unknown action tooltip stays unchanged", () => Equal(descriptions[0].Text,PotencyTooltip.Append(119,descriptions[0].Text)));
// Installed client macros select these lower-level values before trait unlocks.
Test("pre-94 Dia evaluated potency totals 715", () => Equal(715,PotencyTooltip.Parse(16532,"Deals unaspected damage with a potency of 65.\nAdditional Effect: Unaspected damage over time\nPotency: 65\nDuration: 30s")?.Total));
Test("pre-85 Regen evaluated potency totals 1200", () => Equal(1200,PotencyTooltip.Parse(137,"Grants healing over time effect to target.\nCure Potency: 200\nDuration: 18s")?.Total));
// Multi-job tooltip tests
var multiJobTooltips = new (uint Action, string Text, int Initial, int Tick, int Ticks, int Total, bool Heal, bool Party)[] {
    (23, "Delivers an attack with a potency of 140 to all nearby enemies.\nAdditional Effect: Damage over time\nPotency: 30\nDuration: 15s", 140, 30, 5, 290, false, false),
    (16540, "Deals unaspected damage over time.\nPotency: 85\nDuration: 30s", 0, 85, 10, 850, false, false),
    (3569, "Envelops a designated area in a veil of succor, granting healing over time to self and any party members who enter.\nCure Potency: 100\nDuration: 24s", 0, 100, 8, 800, true, true),
    (36986, "Deals lightning damage with a potency of 150.\nAdditional Effect: Lightning damage over time\nPotency: 60\nDuration: 30s", 150, 60, 10, 750, false, false),
    (7489, "Delivers an attack with a potency of 200.\nAdditional Effect: Damage over time\nPotency: 50\nDuration: 60s", 200, 50, 20, 1200, false, false),
    (188, "Creates a designated area in which party members will only suffer 90% of all damage inflicted.\nDuration: 15s\nAdditional Effect: Regen\nCure Potency: 100", 0, 100, 5, 500, true, true),
    (7406, "Delivers an attack with a potency of 150.\nAdditional Effect: Poison\nPotency: 20\nDuration: 45s\nAdditional Effect: 35% chance of granting Hawk's Eye\nDuration: 30s", 150, 20, 15, 450, false, false),
};

foreach (var ex in multiJobTooltips) {
    Test($"multi-job action {ex.Action} breakdown", () => {
        var parsed = PotencyTooltip.Parse(ex.Action, ex.Text) ?? throw new Exception($"failed to parse action {ex.Action}");
        Equal(ex.Initial, parsed.Initial);
        Equal(ex.Tick, parsed.Tick);
        Equal(ex.Ticks, parsed.TickCount);
        Equal(ex.Total, parsed.Total);
        Equal(ex.Heal, parsed.Healing);
        Equal(ex.Party, parsed.PerRecipient);
    });
    Test($"multi-job action {ex.Action} append output", () => {
        var result = PotencyTooltip.Append(ex.Action, ex.Text);
        True(result.StartsWith(ex.Text));
        True(result.Contains(ex.Total.ToString()));
        True(result.Contains("full duration"));
        if (ex.Party) True(result.Contains("per recipient"));
        Equal(result, PotencyTooltip.Append(ex.Action, result));
    });
}


// Breaks caught: wrong native display branch, executable-flag mutation, leaked substitutions, duplicate renderer call, invalid slot writes.
Test("native inputs show countdown without disabling execution", () => { unsafe {
    int* data = stackalloc int[21];
    for(int i=0;i<21;i++) data[i]=1000+i;
    data[2]=48; data[5]=16532; // slot starts at index 2
    var before=new ReadOnlySpan<int>(data,21).ToArray(); int calls=0;
    True(UiCooldownOverride.Invoke(data,21,2,new(40,18),()=> {
        calls++; Equal(3,data[4]); Equal(0,data[9]); Equal(40,data[10]); Equal(100,data[11]); Equal(18,data[12]); Equal(1,data[14]);
        Equal(before[7],data[7]); Equal(before[8],data[8]); Equal(before[2],data[2]); Equal(before[5],data[5]);
    }));
    Equal(1,calls); True(new ReadOnlySpan<int>(data,21).SequenceEqual(before));
} });
Test("native inputs restore after renderer throws", () => { unsafe {
    int* data=stackalloc int[17]; new Span<int>(data,17).Fill(100); data[0]=48;data[3]=16532;
    var before=new ReadOnlySpan<int>(data,17).ToArray();int calls=0;bool thrown=false;
    try{UiCooldownOverride.Invoke(data,17,0,new(40,18),()=>{calls++;throw new InvalidOperationException("renderer failure");});}
    catch(InvalidOperationException){thrown=true;}
    True(thrown);Equal(1,calls);True(new ReadOnlySpan<int>(data,17).SequenceEqual(before));
} });
Test("native inputs preserve unrelated renderer writes", () => { unsafe {
    int* data=stackalloc int[17];new Span<int>(data,17).Fill(100);data[0]=48;data[3]=16532;
    UiCooldownOverride.Invoke(data,17,0,new(40,18),()=>data[14]=7);
    Equal(7,data[14]);Equal(100,data[8]);
} });
foreach(var (length,index,type,action) in new (int,int,int,int)[]{(16,0,48,16532),(17,-1,48,16532),(17,int.MaxValue,48,16532),(17,0,47,16532),(17,0,48,119)})
    Test($"invalid native slot {length}/{index}/{type}/{action} is untouched",()=>{unsafe{
        int* data=stackalloc int[17];new Span<int>(data,17).Fill(100);data[0]=type;data[3]=action;
        var before=new ReadOnlySpan<int>(data,17).ToArray();int calls=0;
        Equal(false,UiCooldownOverride.Invoke(data,length,index,new(40,18),()=>calls++));Equal(1,calls);
        True(new ReadOnlySpan<int>(data,17).SequenceEqual(before));
    }});
Test("null native array passes renderer through once",()=>{unsafe{int calls=0;Equal(false,UiCooldownOverride.Invoke(null,17,0,new(40,18),()=>calls++));Equal(1,calls);}});
Test("fresh native sweep remains visible",()=>{unsafe{
    int* data=stackalloc int[17];new Span<int>(data,17).Fill(100);data[0]=48;data[3]=16532;
    UiCooldownOverride.Invoke(data,17,0,new(0,30),()=>Equal(1,data[8]));
}});

// Break caught: rejecting actual standard hotbar identities because the client reports the cross flag as true.
foreach(var name in new[]{"_ActionBar","_ActionBar01","_ActionBar02","_ActionBar03","_ActionBar04","_ActionBar05","_ActionBar06","_ActionBar07","_ActionBar08","_ActionBar09"})
    foreach(var reportedCross in new[]{false,true})
        Test($"standard hotbar {name} accepted with reported cross={reportedCross}",()=>True(UiCooldownOverride.SupportsAddon(name,reportedCross)));
foreach(var name in new string?[]{null,"","_ActionCross","_ActionDoubleCrossL","_ActionDoubleCrossR","_ActionBar00","_ActionBar10","_ActionBar01Extra","ActionDetail"})
    foreach(var reportedCross in new[]{false,true})
        Test($"nonstandard addon {name} rejected with reported cross={reportedCross}",()=>Equal(false,UiCooldownOverride.SupportsAddon(name,reportedCross)));

// Breaks caught: shifted defaults, unbounded/nonfinite config, cumulative transforms, stale-node writes, lost original layout.
var baselineLayout=new TextNodeLayout(10,20,1.25f,0.75f);
Test("countdown style defaults preserve native layout",()=>Equal(baselineLayout,CountdownTextLayout.Apply(baselineLayout,CountdownTextStyle.Default)));
Test("countdown text offsets are relative to native layout",()=>Equal(new TextNodeLayout(22,12,1.25f,0.75f),CountdownTextLayout.Apply(baselineLayout,new(12,-8,1))));
Test("countdown text size preserves native scale proportions",()=>Equal(new TextNodeLayout(10,20,2.5f,1.5f),CountdownTextLayout.Apply(baselineLayout,new(0,0,2))));
Test("countdown style finite settings are preserved",()=>Equal(new CountdownTextStyle(12,-8,1.5f),new CountdownTextStyle(12,-8,1.5f).Normalize()));
Test("countdown style clamps excessive settings",()=>Equal(new CountdownTextStyle(100,-100,3),new CountdownTextStyle(200,-200,5).Normalize()));
Test("countdown style clamps minimum size",()=>Equal(0.5f,new CountdownTextStyle(0,0,-1).Normalize().Size));
foreach(var invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
    Test($"countdown style replaces invalid {invalid} with defaults",()=>Equal(CountdownTextStyle.Default,new CountdownTextStyle(invalid,invalid,invalid).Normalize()));
Test("reset style returns to exact native layout",()=>Equal(baselineLayout,CountdownTextLayout.Apply(baselineLayout,CountdownTextStyle.Default)));
Test("text layout tracker restores original exactly once",()=>{
    var tracker=new CountdownLayoutTracker();var key=new TextSlotKey(3,2);
    tracker.Remember(key,123,baselineLayout);
    Equal<TextNodeLayout?>(baselineLayout,tracker.Take(key,123));Equal<TextNodeLayout?>(null,tracker.Take(key,123));
});
Test("text layout tracker retains first original during repeated styling",()=>{
    var tracker=new CountdownLayoutTracker();var key=new TextSlotKey(3,2);
    tracker.Remember(key,123,baselineLayout);tracker.Remember(key,123,new(99,99,3,3));
    Equal<TextNodeLayout?>(baselineLayout,tracker.Take(key,123));
});
Test("text layout tracker cannot restore to replaced native node",()=>{
    var tracker=new CountdownLayoutTracker();var key=new TextSlotKey(3,2);tracker.Remember(key,123,baselineLayout);
    Equal<TextNodeLayout?>(null,tracker.Take(key,456));Equal<TextNodeLayout?>(null,tracker.Take(key,123));
});
Test("text layout tracker uses new original for replacement node",()=>{
    var tracker=new CountdownLayoutTracker();var key=new TextSlotKey(3,2);var replacement=new TextNodeLayout(1,2,1,1);
    tracker.Remember(key,123,baselineLayout);tracker.Remember(key,456,replacement);
    Equal<TextNodeLayout?>(replacement,tracker.Take(key,456));
});
Test("finalizing one addon cannot discard another layout",()=>{
    var tracker=new CountdownLayoutTracker();tracker.Remember(new(3,0),123,baselineLayout);tracker.Remember(new(4,0),456,baselineLayout);
    tracker.ForgetAddon(3);Equal<TextNodeLayout?>(null,tracker.Take(new(3,0),123));Equal<TextNodeLayout?>(baselineLayout,tracker.Take(new(4,0),456));
});
Test("restoring layout each frame prevents cumulative offsets and scale",()=>{
    var tracker=new CountdownLayoutTracker();var key=new TextSlotKey(3,2);var current=baselineLayout;
    for(int i=0;i<5;i++){
        current=tracker.Take(key,123) ?? current;tracker.Remember(key,123,current);
        current=CountdownTextLayout.Apply(current,new(12,-8,2));
        Equal(new TextNodeLayout(22,12,2.5f,1.5f),current);
    }
    Equal<TextNodeLayout?>(baselineLayout,tracker.Take(key,123));
});

int failed=0;
foreach(var (name,run) in tests) {
    try {run();Console.WriteLine($"PASS {name}");}
    catch(Exception ex){failed++;Console.WriteLine($"FAIL {name}: {ex.Message}");}
}
Console.WriteLine($"{tests.Count-failed}/{tests.Count} passed");
return failed == 0 ? 0 : 1;
