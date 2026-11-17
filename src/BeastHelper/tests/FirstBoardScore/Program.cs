using BeastNav;
using BeastNav.Models;
using BeastNav.Services;
try { Run(); } catch (Exception e) { Console.Error.WriteLine($"FAILED: {e.Message}"); Environment.ExitCode=1; }
static void Run()
{
 int count=0;
 void Check(bool b,string m) { if(!b) throw new Exception(m); count++; }
 var c=new Configuration { AutomationStopped=false, FirstBoardScoreEnabled=true,CombatOnlyEnabled=false,AutoEnterCrucibleEnabled=true,CrucibleAutoDutyModeEnabled=true,AutoSelectBoardEnabled=true,SelectedCrucibleBoard="wrong board",AutoCrucibleShopBuyEnabled=true,AutoRecoveryItemEnabled=true,AutoSelfBuffItemsEnabled=true,AutoOffensiveItemsEnabled=true,AutoCrucibleItemRulesEnabled=true };
 FirstBoardScorePolicy.Enforce(c);
 Check(c.CombatOnlyEnabled&&c.CombatOnlyBeastSelectionEnabled&&c.AutoCruciblePetPartyEnabled&&c.AutoCrucibleWhistleAssignEnabled,"Combat-only roster and whistles required");
 Check(!c.AutoEnterCrucibleEnabled&&!c.CrucibleAutoDutyModeEnabled&&!c.AutoSelectBoardEnabled&&!c.AutoCrucibleShopBuyEnabled,"Entry, duty handoff and shops blocked");
 Check(!c.AutoRecoveryItemEnabled&&!c.AutoSelfBuffItemsEnabled&&!c.AutoOffensiveItemsEnabled&&!c.AutoCrucibleItemRulesEnabled,"Every item path blocked");
 Check(c.SelectedCrucibleBoard==new Configuration().SelectedCrucibleBoard,"First Board selected");
 c.AutoRecoveryItemEnabled=true;FirstBoardScorePolicy.Enforce(c);Check(!c.AutoRecoveryItemEnabled,"Cannot reenable item use");
 c.FirstBoardScoreEnabled=false;c.AutoRecoveryItemEnabled=true;FirstBoardScorePolicy.Enforce(c);Check(c.AutoRecoveryItemEnabled,"Other modes unchanged");
 Check(FirstBoardScorePolicy.AllowsCombat(1339)&&!FirstBoardScorePolicy.AllowsCombat(1342)&&!FirstBoardScorePolicy.AllowsCombat(1343),"First Board combat only");
 c.FirstBoardScoreEnabled=true;c.ScoreBoardTerritory=1340;
 FirstBoardScorePolicy.Enforce(c);
 Check(c.SelectedCrucibleBoard=="第二盤","Second Board selected explicitly");
 Check(FirstBoardScorePolicy.AllowsCombat(c,1340)&&!FirstBoardScorePolicy.AllowsCombat(c,1339)&&!FirstBoardScorePolicy.AllowsCombat(c,1343),"Selected board alone may run score automation");
 Check(!c.AutoRecoveryItemEnabled&&!c.AutoCrucibleShopBuyEnabled&&!c.AutoEnterCrucibleEnabled,"Second Board retains no-item, no-shop and manual-entry policy");
 c.ScoreBoardTerritory=1341;c.AutoRecoveryItemEnabled=true;c.AutoOffensiveItemsEnabled=true;c.AutoSelfBuffItemsEnabled=true;c.AutoCrucibleItemRulesEnabled=true;
 FirstBoardScorePolicy.Enforce(c);
 Check(c.SelectedCrucibleBoard=="第三盤"&&FirstBoardScorePolicy.AllowsCombat(c,1341)&&!FirstBoardScorePolicy.AllowsCombat(c,1340),"Third Board selected and isolated");
 Check(FirstBoardScorePolicy.ItemsAllowed(c)&&c.AutoRecoveryItemEnabled&&c.AutoOffensiveItemsEnabled,"Third Board permits recovery and attack items");
 Check(!c.AutoEnterCrucibleEnabled&&!c.AutoSelectBoardEnabled&&!c.AutoCrucibleShopBuyEnabled&&!c.AutoSelfBuffItemsEnabled&&!c.AutoCrucibleItemRulesEnabled,"Third Board entry shops and arbitrary consumables remain manual or off");
 c.AutoRecoveryItemEnabled=false;c.AutoOffensiveItemsEnabled=false;FirstBoardScorePolicy.Enforce(c);
 Check(!c.AutoRecoveryItemEnabled&&!c.AutoOffensiveItemsEnabled,"Third Board respects independently disabled item switches");
 c.ScoreBoardTerritory=1340;c.AutoRecoveryItemEnabled=true;c.AutoOffensiveItemsEnabled=true;FirstBoardScorePolicy.Enforce(c);
 Check(!FirstBoardScorePolicy.ItemsAllowed(c)&&!c.AutoRecoveryItemEnabled&&!c.AutoOffensiveItemsEnabled,"Returning to score board restores no-item restriction");
 c.ScoreBoardTerritory=999;Check(!FirstBoardScorePolicy.AllowsCombat(c,999),"Invalid board fails closed");
 c.ScoreBoardTerritory=1339;Check(FirstBoardScorePolicy.AllowsCombat(c,1339)&&!FirstBoardScorePolicy.AllowsCombat(c,1340),"First Board remains isolated");
 var pets=Enumerable.Range(1,8).Select(i=>new BeastPet {PetRowId=(uint)i,XbmRowId=(uint)i,Classification=(byte)(i<5?1:8),Rank=5,Stat1=(byte)i}).ToArray();
 var ranks=pets.ToDictionary(p=>p.PetRowId,_=>new BeastProgress {Level=5});
 var team=FirstBoardScorePolicy.Select(pets,ranks,0,[],out _);
 Check(team.Count==3&&team.Select(p=>p.Classification).Distinct().Count()==1,"Exactly three same-family beasts");
 Check(team.All(p=>p.Classification==8),"Highest estimated trio across families");
 var locked=team.Select(p=>p.PetRowId).ToArray();ranks[1]=new BeastProgress {Level=25};
 Check(FirstBoardScorePolicy.Select(pets,ranks,0,locked,out _).Select(p=>p.PetRowId).SequenceEqual(locked),"Ranks do not change locked trio");
 Check(FirstBoardScorePolicy.Select(pets.Reverse().Concat(pets),ranks,0,[],out _).Select(p=>p.PetRowId).SequenceEqual(locked),"Input order, duplicates and capped rank deterministic");
 Check(FirstBoardScorePolicy.Select(pets,ranks,1,[],out _).All(p=>p.Classification==1),"Family preference honored");
 Check(FirstBoardScorePolicy.Select(pets,ranks,2,[],out _).Count==0,"Missing family pauses");
 Check(FirstBoardScorePolicy.Select(pets,ranks,0,[5,5,6],out _).Count==0,"Duplicate locked beast rejected");
 Check(FirstBoardScorePolicy.Select(pets,ranks,0,[1,5,6],out _).Count==0,"Mixed family rejected");
 Check(FirstBoardScorePolicy.Select(pets.Where(p=>p.PetRowId!=locked[0]),ranks,0,locked,out _).Count==0,"Missing locked beast pauses without replacement");
 Check(FirstBoardScorePolicy.Select(pets,new Dictionary<uint,BeastProgress>(),0,[],out _).Count==0,"Unknown ranks pause");
 Check(FirstBoardScorePolicy.Select(pets,ranks,1,locked,out _).Count==0,"Locked family mismatch rejected");
 Check(FirstBoardScorePolicy.Select(pets,ranks,0,[1,2,3,4],out _).Count==0,"Fourth beast rejected");
 var bugs = new[] {44u,16u,10u}.Select((id,i)=>new BeastPet {XbmRowId=id,PetRowId=id+47,Classification=2}).ToArray();
 Check(FirstBoardScorePolicy.VilekinTrio(bugs.Reverse(),out _).Select(p=>p.XbmRowId).SequenceEqual(new uint[]{44,16,10}),"Named trio and battlehorn order exact");
 Check(FirstBoardScorePolicy.VilekinTrio(bugs.Take(2),out _).Count==0,"Missing named bug never substitutes another vilekin");
 Check(FirstBoardScorePolicy.VilekinTrio(bugs.Select(p=>p with {Classification=1}),out _).Count==0,"Incorrect family blocks named trio");
 foreach(var pet in new[]{44,16}) {
  Check(RotationSolver.Basic.Helpers.FirstBoardVilekinPolicy.Release(pet,true,true,false,1),"Poison/debuff opener releases immediately");
  Check(!RotationSolver.Basic.Helpers.FirstBoardVilekinPolicy.Dismiss(pet,false,false),"New summon cannot be dismissed before Nature was observed");
  Check(!RotationSolver.Basic.Helpers.FirstBoardVilekinPolicy.Dismiss(pet,true,true),"Failed release retaining Nature cannot advance");
  Check(RotationSolver.Basic.Helpers.FirstBoardVilekinPolicy.Dismiss(pet,true,false),"Observed Nature consumption advances summon sequence");
 }
 Check(!RotationSolver.Basic.Helpers.FirstBoardVilekinPolicy.Release(10,true,true,false,0.31f),"Final Sting held above finisher threshold");
 Check(RotationSolver.Basic.Helpers.FirstBoardVilekinPolicy.Release(10,true,true,false,0.3f),"Final Sting available at finisher threshold");
 Check(!RotationSolver.Basic.Helpers.FirstBoardVilekinPolicy.Release(10,true,true,true,0.1f),"Never spend finisher into parry");
 Check(!RotationSolver.Basic.Helpers.FirstBoardVilekinPolicy.Release(10,false,true,false,0.1f),"No out-of-combat finisher");
 Check(!RotationSolver.Basic.Helpers.FirstBoardVilekinPolicy.Dismiss(10,true,true),"Do not prematurely dismiss finisher pet");
 c.AutomationStopped=true;c.FirstBoardScoreEnabled=true;c.CombatOnlyEnabled=false;
 FirstBoardScorePolicy.Enforce(c);
 Check(c.AutomationStopped&&!c.CombatOnlyEnabled,"Profile enforcement cannot resume stopped automation");
 var saved=System.Text.Json.JsonSerializer.Serialize(c);
 Check(System.Text.Json.JsonSerializer.Deserialize<Configuration>(saved)!.AutomationStopped,"Stop latch survives config reload");
 Check(new Configuration().AutomationStopped,"New stop-capable build starts stopped until explicit Resume");
 uint[] desired={44,16,10};
 Check(WhistleSelectionPolicy.NextToggle(desired,desired)==0,"Auto-selected correct trio must never toggle off");
 var starts=new uint[][] { [],[44],[44,16],[10,16,44],[16,10,44],[44,10,16],[10],[16,44] };
 foreach(var initial in starts) {
  var current=initial;var actions=0;
  while(!WhistleSelectionPolicy.Ready(current,desired)&&actions<7) {
   var toggle=WhistleSelectionPolicy.NextToggle(current,desired);
   Check(toggle!=0,"Valid mismatch has a bounded correction");
   current=WhistleSelectionPolicy.AfterToggle(current,toggle);actions++;
  }
  Check(WhistleSelectionPolicy.Ready(current,desired)&&actions<=6,"Partial or reversed slots converge to exact order");
  Check(WhistleSelectionPolicy.NextToggle(current,desired)==0,"Repeated ready ticks are idempotent");
 }
 Check(WhistleSelectionPolicy.NextToggle([44,44],desired)==0,"Invalid duplicate read must never cause a blind toggle");
 Check(WhistleSelectionPolicy.NextToggle([0],desired)==0,"Invalid native ID must pause");
 Check(BossMod.Global.CrucibleOfTheUnbroken.SecondBoard.SecondBoardTargetPolicy.HealthDifference(10,100,90,100)<0,"Balance targets elder when younger is weaker, without unsigned underflow");
 Check(Math.Abs(BossMod.Global.CrucibleOfTheUnbroken.SecondBoard.SecondBoardTargetPolicy.HealthDifference(50,100,100,200))<0.0001,"Compare percentages rather than raw HP");
 Check(BossMod.Global.CrucibleOfTheUnbroken.SecondBoard.SecondBoardTargetPolicy.HealthDifference(90,100,10,100)>0,"Balance targets younger when elder is weaker");
 Check(WhistleSelectionPolicy.ResolveIndices([10u,44u,16u],[1u,2u,0u])!.SequenceEqual(new uint[] {44,16,10}),"Whistle vector stores roster indices, not beast identities");
 Check(WhistleSelectionPolicy.ResolveIndices([10u,44u,16u],[0u,1u,2u])!.SequenceEqual(new uint[] {10,44,16}),"Game-registered horn order preserved");
 Check(WhistleSelectionPolicy.ResolveIndices([10u,44u,16u],[3u])==null,"Out of range horn index pauses");
 Check(WhistleSelectionPolicy.ResolveIndices([10u,44u,16u],[0u,0u])==null,"Duplicate horn index rejected");
 Check(WhistleSelectionPolicy.SameTrio([10u,44u,16u],[44u,16u,10u]),"Game automatically registered trio must never be deselected for sorting");
 Check(!WhistleSelectionPolicy.SameTrio([44u,44u,10u],[44u,16u,10u]),"Missing species never accepted");
 Check(BossMod.Global.CrucibleOfTheUnbroken.SecondBoardAssistPolicy.Allow(true,1340,1089,true),"Board Two WIP opt-in accepted only in correct context");
 Check(!BossMod.Global.CrucibleOfTheUnbroken.SecondBoardAssistPolicy.Allow(false,1340,1089,true)&&!BossMod.Global.CrucibleOfTheUnbroken.SecondBoardAssistPolicy.Allow(true,1339,1089,true)&&!BossMod.Global.CrucibleOfTheUnbroken.SecondBoardAssistPolicy.Allow(true,1340,1092,true)&&!BossMod.Global.CrucibleOfTheUnbroken.SecondBoardAssistPolicy.Allow(true,1340,1089,false),"WIP opt-in cannot affect unrelated content");
 Check(BossMod.Global.CrucibleOfTheUnbroken.SecondBoard.SecondBoardTargetPolicy.DoomTileCharging(0x02000100),"Taurus cleanse-ready tile attracts movement");
 Check(!BossMod.Global.CrucibleOfTheUnbroken.SecondBoard.SecondBoardTargetPolicy.DoomTileCharging(0x10000800)&&!BossMod.Global.CrucibleOfTheUnbroken.SecondBoard.SecondBoardTargetPolicy.DoomTileCharging(0x00080004),"Finished or vanished Taurus tile cannot retain false charging priority");
 Check(RosterEditPolicy.IsComplete([10u,44u,16u],[44u,16u,10u]),"Correct roster yields to manual entry regardless of order");
 Check(!RosterEditPolicy.IsComplete([10u,44u],[44u,16u,10u]),"Missing beast still permits roster repair");
 Check(!RosterEditPolicy.IsComplete([10u,10u,16u],[44u,16u,10u]),"Duplicate roster is not complete");
 Check(!RosterEditPolicy.MayResetFilters(false,false),"Completed roster never resets filters");
 Check(RosterEditPolicy.MayResetFilters(true,false),"Missing beast permits one search reset");
 for (var frame=0;frame<1000;frame++) Check(!RosterEditPolicy.MayResetFilters(true,true),"Later frames cannot rebuild the notebook again");
 Check(RotationSolver.Basic.Helpers.FirstBoardVilekinPolicy.Release(10,true,true,false,.8f,5f),"Wespe releases before vulnerability expires including pet execution delay");
 Check(!RotationSolver.Basic.Helpers.FirstBoardVilekinPolicy.Release(10,true,true,false,.8f,6f),"Wespe stays reserved earlier in vulnerability window");
 Check(!RotationSolver.Basic.Helpers.FirstBoardVilekinPolicy.Release(10,true,true,true,.8f,3f),"Vulnerability window never overrides parry guard");
 Check(RotationSolver.Basic.Helpers.FirstBoardVilekinPolicy.Release(10,true,true,false,.8f,0,true),"Endangered Wespe may finish before losing the familiar");
 Check(!RotationSolver.Basic.Helpers.FirstBoardVilekinPolicy.Release(10,true,false,false,.8f,3f),"Release still requires One with Nature");
 Check(WhistleSelectionPolicy.MayRetainPartial(true,[10u],[44u,16u,10u]),"Third Board retains remaining selected beast instead of deselecting it");
 Check(!WhistleSelectionPolicy.MayRetainPartial(false,[10u],[44u,16u,10u]),"Score runs keep full-whistle requirement");
 Check(!WhistleSelectionPolicy.MayRetainPartial(true,[],[44u,16u,10u]),"No-beast setup cannot be silently accepted");
 Check(!WhistleSelectionPolicy.MayRetainPartial(true,[99u],[44u,16u,10u]),"Fourth or foreign beast cannot enter partial setup");
 Check(RotationSolver.Basic.Helpers.FirstBoardVilekinPolicy.HoldYmirReserve(true,10,true,.8f),"Keep healthy Wespe while both Ymir bosses remain alive");
 Check(!RotationSolver.Basic.Helpers.FirstBoardVilekinPolicy.HoldYmirReserve(true,10,false,.8f),"Release finisher is permitted after one boss dies");
 Check(!RotationSolver.Basic.Helpers.FirstBoardVilekinPolicy.HoldYmirReserve(false,10,true,.8f),"Ymir reserve cannot affect other boards");
 Check(!RotationSolver.Basic.Helpers.FirstBoardVilekinPolicy.HoldYmirReserve(true,10,true,.1f),"Dying familiar can still deliver its finisher");
 var kin=new uint[]{33,1,26}.Select(id=>new BeastPet {XbmRowId=id,PetRowId=id+47,Classification=1}).ToArray();
 Check(FirstBoardScorePolicy.BeastkinTrio(kin,out _).Select(p=>p.XbmRowId).SequenceEqual(new uint[]{1,26,33}),"Beastkin trio is exact and independent of notebook order");
 Check(FirstBoardScorePolicy.BeastkinTrio(kin.Take(2),out _).Count==0,"Missing Beastkin cannot substitute a fourth species");
 var kinConfig=new Configuration {FirstBoardScoreEnabled=true,ScoreBoardTerritory=1341,ThirdBoardBeastkinClear=true};
 Check(FirstBoardScorePolicy.BeastkinClear(kinConfig),"Third Board Beastkin opt-in active");
 kinConfig.ScoreBoardTerritory=1340;Check(!FirstBoardScorePolicy.BeastkinClear(kinConfig),"Beastkin rotation cannot affect Second Board");
 Check(RotationSolver.Basic.Helpers.ThirdBoardBeastkinPolicy.Opening(true,true,true,true,false)==1,"Borrow defense starts with Cu Sith");
 Check(RotationSolver.Basic.Helpers.ThirdBoardBeastkinPolicy.Opening(false,true,true,true,true)==26,"Ymir exposure prepares Buffalo");
 Check(RotationSolver.Basic.Helpers.ThirdBoardBeastkinPolicy.Opening(false,false,false,false,false)==0,"No ready horn retains rather than dismisses");
 Check(!RotationSolver.Basic.Helpers.ThirdBoardBeastkinPolicy.PrepareBuffalo(true,false,.8f,false),"Buffalo held out of early shell damage");
 Check(RotationSolver.Basic.Helpers.ThirdBoardBeastkinPolicy.PrepareBuffalo(true,false,.4f,false),"Buffalo prepares before shell breaks");
 Check(!RotationSolver.Basic.Helpers.ThirdBoardBeastkinPolicy.PrepareBuffalo(true,true,.4f,true),"Buffalo stun not spent twice");
 Check(!RotationSolver.Basic.Helpers.ThirdBoardBeastkinPolicy.Release(26,true,true,false,true,false),"Buffalo does not waste stun against protected Ymir");
 Check(!RotationSolver.Basic.Helpers.ThirdBoardBeastkinPolicy.Release(26,true,true,true,false,true),"Buffalo stun requires familiar in range");
 Check(RotationSolver.Basic.Helpers.ThirdBoardBeastkinPolicy.Release(26,true,true,true,true,false),"Buffalo stuns exposed Ymir");
 Check(RotationSolver.Basic.Helpers.ThirdBoardBeastkinPolicy.Release(33,true,true,false,true,false),"Coeurl uses controlled attack for sustained damage");
 Check(!RotationSolver.Basic.Helpers.ThirdBoardBeastkinPolicy.Release(1,true,false,true,true,false),"Cu Sith reserves Nature for Borrow before Beastskin");
 var releaseTime=DateTime.UtcNow;
 Check(!RotationSolver.Basic.Helpers.ThirdBoardBeastkinPolicy.CanSwap(releaseTime.AddSeconds(3.3),releaseTime.AddSeconds(4),default),"Do not dismiss pet before delayed controlled hit");
 Check(RotationSolver.Basic.Helpers.ThirdBoardBeastkinPolicy.CanSwap(releaseTime.AddSeconds(4),releaseTime.AddSeconds(4),default),"Swap allowed after controlled ability completes");
 Check(!RotationSolver.Basic.Helpers.ThirdBoardBeastkinPolicy.CanSwap(releaseTime.AddSeconds(4),releaseTime.AddSeconds(4),releaseTime.AddSeconds(5)),"Defensive hold still prevents dismissal");
 Check(RotationSolver.Basic.Helpers.ThirdBoardBeastkinPolicy.NextSwap(1,.9f,false,false,false,true,true)==0,"Healthy Cu Sith remains after Borrow instead of being sacrificed");
 Check(RotationSolver.Basic.Helpers.ThirdBoardBeastkinPolicy.NextSwap(33,.9f,false,false,true,false,true)==0,"Healthy Coeurl keeps sustained pet damage after release");
 Check(RotationSolver.Basic.Helpers.ThirdBoardBeastkinPolicy.NextSwap(33,.1f,false,false,false,false,true)==26,"Buffalo is usable outside Ymir when another pet is endangered");
 Check(RotationSolver.Basic.Helpers.ThirdBoardBeastkinPolicy.NextSwap(33,.1f,false,true,false,false,true)==0,"Ymir Buffalo reserve is preserved");
 Check(RotationSolver.Basic.Helpers.ThirdBoardBeastkinPolicy.NextSwap(1,.9f,true,true,false,true,true)==26,"Ymir shell-break swap still takes priority");
 Check(RotationSolver.Basic.Helpers.ThirdBoardDefensePolicy.Incoming(0x4CA1,48563),"Siren Song of Torment requests cover before damage");
 Check(RotationSolver.Basic.Helpers.ThirdBoardDefensePolicy.Incoming(0x4C9E,48561),"Lakhamu Sand Tempest requests cover");
 Check(RotationSolver.Basic.Helpers.ThirdBoardDefensePolicy.Incoming(0x4C93,48479)&&RotationSolver.Basic.Helpers.ThirdBoardDefensePolicy.Incoming(0x4C95,48485),"Existing Ymir cast defense retained");
 Check(!RotationSolver.Basic.Helpers.ThirdBoardDefensePolicy.Incoming(0x4CA1,48571),"Forced march cannot incorrectly trigger tankbuster cover");
 Check(!RotationSolver.Basic.Helpers.ThirdBoardDefensePolicy.Incoming(0x4C93,48563),"Cast protection restricted to correct encounter actor");
 Check(RotationSolver.Basic.Helpers.ThirdBoardDefensePolicy.Cover(.63f,.686f,false),"Healthy-enough Coeurl below 70 percent can share incoming damage");
 Check(!RotationSolver.Basic.Helpers.ThirdBoardDefensePolicy.Cover(.8f,.17f,false),"Weak starting Cu Sith is not automatically tanked during ordinary attacks");
 Check(RotationSolver.Basic.Helpers.ThirdBoardDefensePolicy.TakeBack(.65f,.25f,false),"Player takes damage back from endangered familiar");
 Check(!RotationSolver.Basic.Helpers.ThirdBoardDefensePolicy.TakeBack(.15f,.05f,true),"Critically low player does not remove protective cover");
 Check(RotationSolver.Basic.Helpers.ThirdBoardDefensePolicy.Incoming(0x4CAA,48620),"Guttler Thunderbolt requests cast-timed pet cover when a pet is available");
 Check(RotationSolver.Basic.Helpers.ThirdBoardDefensePolicy.ReserveGapCloser(0x4CAC,48619),"Molten Blade proximity cast prevents gap-closing back into danger");
 Check(!RotationSolver.Basic.Helpers.ThirdBoardDefensePolicy.ReserveGapCloser(0x4CAA,48620),"Thunderbolt does not incorrectly reserve proximity retreat");
 var loot=new LootBoostPreparationPolicy();var lootNow=DateTime.UtcNow;
 Check(loot.Evaluate(1,lootNow,false,false,true,true)==LootBoostStep.Ready,"Disabled or no-item mode never consumes loot item");
 Check(loot.Evaluate(1,lootNow,true,true,true,true)==LootBoostStep.Ready,"Existing Winners Keepers never wastes another eye");
 Check(loot.Evaluate(1,lootNow,true,false,false,false)==LootBoostStep.Ready,"Missing eye never blocks combat");
 Check(loot.Evaluate(1,lootNow,true,false,true,true)==LootBoostStep.Use,"Held usable eye is prepared before pull");
 loot.Requested(lootNow);
 Check(loot.Evaluate(1,lootNow.AddSeconds(.5),true,false,true,true)==LootBoostStep.Wait,"Allow native item effect time without duplicate use");
 Check(loot.Evaluate(1,lootNow.AddSeconds(1),true,true,true,true)==LootBoostStep.Ready,"Buff confirmation releases combat immediately");
 Check(loot.Evaluate(1,lootNow.AddSeconds(2),true,false,true,true)==LootBoostStep.Use,"Failed request permits one bounded retry");
 loot.Requested(lootNow.AddSeconds(2));
 Check(loot.Evaluate(1,lootNow.AddSeconds(4),true,false,true,true)==LootBoostStep.Wait,"Two requests maximum until deadline");
 Check(loot.Evaluate(1,lootNow.AddSeconds(6),true,false,true,true)==LootBoostStep.TimedOut,"Failed native use cannot permanently freeze battle");
 Check(loot.Evaluate(2,lootNow.AddSeconds(10),true,false,true,true)==LootBoostStep.Use,"New encounter resets bounded preparation");
 Check(loot.Evaluate(2,lootNow.AddSeconds(16),true,false,true,true)==LootBoostStep.TimedOut,"New eye preparation has its own deadline");
 Check(loot.Evaluate(2,lootNow.AddSeconds(17),true,false,true,true)==LootBoostStep.Ready,"Timed-out eye does not flood logs or repeatedly block subsequent frames");
 var merchant=new LootBoostPreparationPolicy();
 Check(merchant.Evaluate(2,lootNow.AddSeconds(17),true,false,true,true)==LootBoostStep.Use,"Merchant eye has independent requests and timeout from Thief eye");
 Check(merchant.Evaluate(2,lootNow.AddSeconds(17),true,true,true,true)==LootBoostStep.Ready,"Token Benefits prevents duplicate Merchant Eye consumption");
 Check(!RotationSolver.Basic.Helpers.ThirdBoardDefensePolicy.PreserveCuCover(1,false),"Healthy player never assigns Cu Sith as routine tank");
 Check(RotationSolver.Basic.Helpers.ThirdBoardDefensePolicy.PreserveCuCover(1,true),"Emergency player protection can still use Cu Sith");
 Check(RotationSolver.Basic.Helpers.ThirdBoardDefensePolicy.PreserveCuTakeBack(1,.7f,false),"Challenge removes ordinary enemy pressure from Cu Sith");
 Check(RotationSolver.Basic.Helpers.ThirdBoardDefensePolicy.ChallengePriority(0x4C8E,true)>RotationSolver.Basic.Helpers.ThirdBoardDefensePolicy.ChallengePriority(0x4C90,true),"Rescue Challenge prioritizes attacking Cavalier above attacking bishop");
 Check(RotationSolver.Basic.Helpers.ThirdBoardBeastkinPolicy.NextSwap(1,.55f,false,false,false,true,true)==33,"Cu Sith can be reserved before critical HP when another horn is ready");
 Check(FamiliarRecoveryPolicy.ShouldHeal(true,false,true,35,50),"Living wounded familiar can receive potion while player is safe");
 Check(!FamiliarRecoveryPolicy.ShouldHeal(true,true,true,35,50),"Player potion priority preserved");
 Check(!FamiliarRecoveryPolicy.ShouldHeal(true,false,true,0,50),"Potion cannot revive dead familiar");
 Check(!FamiliarRecoveryPolicy.ShouldHeal(true,false,false,35,50),"Familiar healing restricted to requested Beastkin mode");
 Check(!FamiliarRecoveryPolicy.ShouldHeal(false,false,true,35,50),"Familiar potion checkbox respected");
 Check(ConditionalRecoveryPolicy.Candidates(false,false,false,false,false).Length==0,"No Ribbon or curable debuff means no conditional items wasted");
 Check(ConditionalRecoveryPolicy.Candidates(true,false,false,false,false).Contains((ushort)91),"Crimson Ribbon enables G2 Antiblind as healing item");
 Check(!ConditionalRecoveryPolicy.Candidates(false,false,false,false,false).Contains((ushort)91),"No confirmed Ribbon never spends serum for healing");
 Check(ConditionalRecoveryPolicy.Candidates(false,false,true,false,false).SequenceEqual(new ushort[]{85}),"Eye Drops require blindness");
 Check(ConditionalRecoveryPolicy.Candidates(false,true,false,false,false).SequenceEqual(new ushort[]{83}),"Antidote requires poison");
 Check(!ConditionalRecoveryPolicy.Candidates(false,false,false,true,false).Contains((ushort)84),"Needle cannot be used as player healing");
 Check(ConditionalRecoveryPolicy.Candidates(false,false,false,true,true).Contains((ushort)84),"Needle heals petrified familiar");
 Check(ConditionalRecoveryPolicy.AreaSerum(91)&&!ConditionalRecoveryPolicy.AreaSerum(90),"G2 uses player-centered area; G1 can target familiar");
 var noStatuses=new HashSet<uint>();var noNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
 Check(new Configuration().AutoEmergencyMitigationEnabled,"Defensive recovery fallback defaults on");
 Check(EmergencyMitigationPolicy.Candidates(true,true,true,noStatuses,noNames).SequenceEqual(new ushort[]{102,137,136,110,112,114,99,98}),"All supported defenses tried in priority order");
 Check(EmergencyMitigationPolicy.Candidates(false,true,true,noStatuses,noNames).Length==0,"Defensive checkbox off consumes nothing");
 Check(EmergencyMitigationPolicy.Candidates(true,false,true,noStatuses,noNames).Length==0,"Healthy target never spends emergency defense");
 Check(EmergencyMitigationPolicy.Candidates(true,true,false,noStatuses,noNames).Length==0,"Healing takes priority over emergency defense");
 Check(!EmergencyMitigationPolicy.Candidates(true,true,true,noStatuses,noNames,true).Contains((ushort)114),"Self-only swiftness never targets familiar");
 Check(EmergencyMitigationPolicy.Candidates(true,true,true,new HashSet<uint>{4857,4849,4862,4863,4846},new HashSet<string>(new[]{"Reflect","Spirit of the Breathtaker"},StringComparer.OrdinalIgnoreCase)).Length==0,"Existing defensive buffs prevent repeated consumption");
 foreach(var item in new ushort[]{102,137,110,112})
 {
     uint status=item switch {102=>4857,137=>4849,110=>4862,_=>4863};
     Check(!EmergencyMitigationPolicy.Candidates(true,true,true,new HashSet<uint>{status},noNames).Contains(item),"Each active status independently suppresses its item");
 }
 Check(!EmergencyMitigationPolicy.Candidates(true,true,true,noStatuses,new HashSet<string>(new[]{"reflect"},StringComparer.OrdinalIgnoreCase)).Contains((ushort)136),"Reflect status comparison is case insensitive");
 Check(!EmergencyMitigationPolicy.Candidates(true,true,true,noStatuses,noNames).Any(i=>i is 111 or 113 or 138 or 139),"Fallback never uses ailment potions, reset sand, or attack sand");
 Check(!EmergencyMitigationPolicy.Candidates(true,true,true,noStatuses,noNames,true).Any(i=>i is 98 or 99),"Reraiser never targets familiar");
 Check(EmergencyMitigationPolicy.Candidates(true,true,true,new HashSet<uint>{4846},noNames).All(i=>i is not 98 and not 99),"Existing Reraise prevents duplicate reraiser");
 Check(!RotationSolver.Basic.Helpers.ThirdBoardDefensePolicy.CanCoverCast(.61f,.43f),"Recorded Siren weak Cu Sith is not forced to absorb Song of Torment");
 Check(RotationSolver.Basic.Helpers.ThirdBoardDefensePolicy.CanCoverCast(.7f,.8f),"Healthy familiar can still cover incoming cast");
 Check(RotationSolver.Basic.Helpers.ThirdBoardDefensePolicy.CanCoverCast(.1f,.2f),"Critical player emergency cover retained");
 Check(BossMod.Global.CrucibleOfTheUnbroken.ThirdBoard.ThirdBoardTargetPolicy.PreferUrgentPrimary(false,44893,3),"Shield Charge prioritizes binding Crawling before zombie cleave count");
 Check(!BossMod.Global.CrucibleOfTheUnbroken.ThirdBoard.ThirdBoardTargetPolicy.PreferUrgentPrimary(false,46750,3),"Challenge can rescue familiar from boss despite priority adds");
 Check(!BossMod.Global.CrucibleOfTheUnbroken.ThirdBoard.ThirdBoardTargetPolicy.PreferUrgentPrimary(false,46751,3),"Snarl target selection remains defensive");
 Check(!BossMod.Global.CrucibleOfTheUnbroken.ThirdBoard.ThirdBoardTargetPolicy.PreferUrgentPrimary(true,46961,3),"Healing never inherits hostile add restriction");
 Check(!BossMod.Global.CrucibleOfTheUnbroken.ThirdBoard.ThirdBoardTargetPolicy.PreferUrgentPrimary(false,44893,1),"Ordinary boss cleave optimization retained");
 Check(!RotationSolver.Basic.Helpers.SirenChargePolicy.Use(false,0,3,false,1f),"Recorded three opening charges on Siren are held");
 Check(RotationSolver.Basic.Helpers.SirenChargePolicy.Use(true,0,3,false,1f),"Crawling can be attacked before bind despite no nearby zombies");
 Check(!RotationSolver.Basic.Helpers.SirenChargePolicy.Use(true,0,1,false,1f),"Last charge reserved before noncritical bind");
 Check(RotationSolver.Basic.Helpers.SirenChargePolicy.Use(true,0,1,true,1f),"Actual bind releases last charge to kill Crawling");
 Check(RotationSolver.Basic.Helpers.SirenChargePolicy.Use(true,0,1,false,.2f),"Low HP releases reserve against approaching binder");
 Check(RotationSolver.Basic.Helpers.SirenChargePolicy.Use(false,3,2,false,.8f),"Spare charge cleaves clustered zombies");
 Check(!RotationSolver.Basic.Helpers.SirenChargePolicy.Use(false,1,3,false,.8f),"Single zombie does not waste reserve as filler");
 Check(!RotationSolver.Basic.Helpers.SirenChargePolicy.Use(false,4,1,false,.8f),"Healthy player retains final charge for next wave");
 Check(RotationSolver.Basic.Helpers.SirenChargePolicy.Use(false,4,1,false,.3f),"Zombie swarm at low HP permits emergency final charge");
 Check(!RotationSolver.Basic.Helpers.SirenChargePolicy.Use(true,4,0,true,.1f),"No charge cannot be requested");
 var xpConfig=new Configuration {FirstBoardScoreEnabled=true,ScoreBoardTerritory=1341,AutoEnterCrucibleEnabled=true,AutoCrucibleShopBuyEnabled=true};
 FirstBoardScorePolicy.SelectXpRun(xpConfig);
 FirstBoardScorePolicy.Enforce(xpConfig);
 Check(FirstBoardScorePolicy.XpRun(xpConfig)&&!xpConfig.FirstBoardScoreEnabled,"XP selection removes three-beast score enforcement");
 Check(xpConfig.SelectedCrucibleBoard=="第三盤"&&xpConfig.CombatOnlyEnabled&&xpConfig.CombatOnlyBeastSelectionEnabled,"XP selects Third Board and enables roster plus whistles");
 Check(!xpConfig.AutoEnterCrucibleEnabled&&!xpConfig.AutoCrucibleShopBuyEnabled&&!xpConfig.CrucibleAutoDutyModeEnabled,"XP preserves manual entry and shops");
 Check(FirstBoardScorePolicy.BeastkinClear(xpConfig),"XP retains established combat-trio behavior");
 xpConfig.FirstBoardScoreEnabled=true;
 Check(!FirstBoardScorePolicy.XpRun(xpConfig),"Explicit score mode can supersede saved XP dropdown");
 var master=new Configuration {CrucibleTeamMode=CrucibleTeamMode.ThirdBoardXpRun,FirstBoardScoreEnabled=true,AutoEnterCrucibleEnabled=true,AutoCrucibleShopBuyEnabled=true};
 FirstMasterLegendaryProfile.Apply(master);
 Check(FirstMasterLegendaryProfile.Active(master)&&!FirstBoardScorePolicy.XpRun(master),"First Master opt-in switches out of XP without score-mode conflict");
 Check(master.SelectedCrucibleBoard=="特一盤"&&master.CombatOnlyEnabled&&master.CombatOnlyBeastSelectionEnabled,"First Master roster and battlehorn preparation enabled");
 Check(!master.AutoEnterCrucibleEnabled&&!master.AutoSelectBoardEnabled&&!master.AutoCrucibleShopBuyEnabled&&!master.CrucibleAutoDutyModeEnabled,"First Master keeps entry and shopping manual");
 Check(master.AutoRecoveryItemHpThreshold==55&&master.AutoEmergencyMitigationEnabled&&master.PrepareRecoveryBeforePull,"First Master survival settings prepared");
 var masterPets=new[]{new BeastPet {PetRowId=47,XbmRowId=1,Classification=1},new BeastPet {PetRowId=97,XbmRowId=50,Classification=1},new BeastPet {PetRowId=80,XbmRowId=33,Classification=1},new BeastPet {PetRowId=73,XbmRowId=26,Classification=1}};
 Check(FirstMasterLegendaryProfile.Team(masterPets,out _).Select(p=>p.XbmRowId).SequenceEqual(new uint[]{1,50,33}),"First Master exactly Cu Sith Behemoth Coeurl; no XP passengers or Buffalo substitution");
 Check(FirstMasterLegendaryProfile.Team(masterPets.Take(1),out var coreMissing).Count==0&&coreMissing.Contains("Behemoth"),"Missing Behemoth pauses preparation");
 Check((int)CrucibleTeamMode.ThirdBoardXpRun==4&&(int)CrucibleTeamMode.FirstMasterLegendary==5,"New First Master mode preserves saved XP enum");
 {
  var second=new Configuration {CrucibleTeamMode=CrucibleTeamMode.ThirdBoardXpRun,FirstBoardScoreEnabled=true,AutoEnterCrucibleEnabled=true,AutoCrucibleShopBuyEnabled=true};
  SecondMasterLegendaryProfile.Apply(second);
  Check(SecondMasterLegendaryProfile.Active(second)&&!FirstMasterLegendaryProfile.Active(second),"Second Master profile does not activate First Master-only logic (Borgny items, RSR First Master IPC, territory 1342)");
  Check(SecondMasterLegendaryProfile.MasterTrioActive(second)&&SecondMasterLegendaryProfile.MasterTrioActive(master),"Both Master profiles field the Cu Sith / Behemoth / Coeurl trio");
  Check(second.SelectedCrucibleBoard=="特二盤"&&second.CombatOnlyEnabled&&second.AutoCrucibleWhistleAssignEnabled,"Second Master roster and battlehorn preparation enabled");
  Check(!second.FirstBoardScoreEnabled&&!second.AutoEnterCrucibleEnabled&&!second.AutoCrucibleShopBuyEnabled&&!second.AutoSelectBoardEnabled,"Second Master keeps entry and shopping manual");
  Check(second.AutoRecoveryItemHpThreshold==55&&second.FamiliarRecoveryHpThreshold==45&&second.AutoOffensiveItemsEnabled&&second.AutoEmergencyMitigationEnabled,"Second Master uses the First Master Legendary item/recovery settings");
  Check((int)CrucibleTeamMode.FirstMasterXpRun==6&&(int)CrucibleTeamMode.SecondMasterLegendary==7,"Second Master mode appended; saved enum values unchanged");
  Check(SecondMasterLegendaryProfile.Territory==1343,"Second Master territory 1343");
  // Second Master attempt 1 (2026-11-13 16:41) fixes
  { var th=CrucibleItemTimingPolicy.RecoveryThreshold(55f,true,1343,true,6980); Check(100f*4929f/6980f<th,"Atomic Ray 17:24 case: cast started at 4,929/6,980 HP - now heals (0.2.38 did not)"); }
  Check(CrucibleItemTimingPolicy.AtomicRayHealBelowHp>4538f*1.1f,"Atomic Ray floor above the largest recorded hit (4,538) plus 10%");
  Check(CrucibleItemTimingPolicy.RecoveryThreshold(55f,true,1343,true,4000)==100f,"Max HP below the floor: heal at any HP during the cast");
  Check(CrucibleItemTimingPolicy.RecoveryThreshold(55f,true,1343,false,6980)==55f&&CrucibleItemTimingPolicy.RecoveryThreshold(55f,false,1343,true,6980)==55f&&CrucibleItemTimingPolicy.RecoveryThreshold(55f,true,1342,true,6980)==55f,"Atomic Ray threshold only on Second Master with the preset");
  Check(!CrucibleItemTimingPolicy.FamiliarHealAllowedAtSecondMasterBoss(true,1343,true)&&CrucibleItemTimingPolicy.FamiliarHealAllowedAtSecondMasterBoss(true,1343,false)&&CrucibleItemTimingPolicy.FamiliarHealAllowedAtSecondMasterBoss(false,1343,true)&&CrucibleItemTimingPolicy.FamiliarHealAllowedAtSecondMasterBoss(true,1342,true),"No familiar potions at Lauda (Second Master preset only)");
  Check(CrucibleItemTimingPolicy.AntidoteAsLastResortHeal(true,1343)&&!CrucibleItemTimingPolicy.AntidoteAsLastResortHeal(true,1342)&&!CrucibleItemTimingPolicy.AntidoteAsLastResortHeal(false,1343),"Antidote as last-resort heal only on Second Master with the preset");
  Check(RotationSolver.Basic.Helpers.SecondMasterDefensePolicy.CoverCast(0x4CDB,49188),"Flauros Erratic Blaster requests cover");
  Check(!RotationSolver.Basic.Helpers.SecondMasterDefensePolicy.CoverCast(0x4CF6,49272),"Durga Atomic Ray not covered (module: pet cover does not work)");
  Check(!RotationSolver.Basic.Helpers.SecondMasterDefensePolicy.CoverCast(0x4CC2,48730),"Second Master list does not cover First Master casts");
  {
   var thrown=BossMod.Global.CrucibleOfTheUnbroken.LaudaUnseenForcePolicy.ThrowDirection(0.55f,true);
   Check(Math.Abs(thrown-(-2.59f))<0.02f,"Unseen Force replay: facing 0.55, opposed -> thrown along -2.59 (observed)");
   Check(BossMod.Global.CrucibleOfTheUnbroken.LaudaUnseenForcePolicy.PocketZ(-425.4f)==BossMod.Global.CrucibleOfTheUnbroken.LaudaUnseenForcePolicy.SouthPocketZ&&BossMod.Global.CrucibleOfTheUnbroken.LaudaUnseenForcePolicy.PocketZ(-410f)==BossMod.Global.CrucibleOfTheUnbroken.LaudaUnseenForcePolicy.NorthPocketZ,"Unseen Force goes to the nearer end pocket");
   var sSafe=BossMod.Global.CrucibleOfTheUnbroken.LaudaUnseenForcePolicy.SafeFacing(-442.5f,true);
   var sThrow=BossMod.Global.CrucibleOfTheUnbroken.LaudaUnseenForcePolicy.ThrowDirection(sSafe,true);
   Check(Math.Abs(sThrow)<0.01f,"From the south pocket the safe facing throws the player north (+z) along the long axis");
   Check(-442.5f+BossMod.Global.CrucibleOfTheUnbroken.LaudaUnseenForcePolicy.KnockbackDistance<=-395f,"South pocket + 45y throw lands inside the north pocket (z <= -395)");
  }
  {
   // Gigantis slimes: replay 18:04 - fire weapon (0x49A), pair spawned, stamp hit the Lightning; Kindling cast Rupture Fire.
   int SP(uint oid,bool rupture,ushort weapon,int alive)=>BossMod.Global.CrucibleOfTheUnbroken.SecondMasterTargetPolicy.Priority(oid,rupture,weapon,alive);
   Check(SP(0x4D07,false,0x49A,2)==3&&SP(0x4D05,false,0x49A,2)==0,"Fire weapon, pair alive: attack the Kindling, leave the Lightning to the stamp");
   Check(SP(0x4D05,false,0x499,2)==3&&SP(0x4D07,false,0x499,2)==0,"Lightning weapon, pair alive: attack the Lightning, leave the Kindling to the stamp");
   Check(SP(0x4D07,false,0x499,1)==3,"After the stamp flips the weapon, the lone spare slime is still attacked (18:23:46)");
   Check(SP(0x4D07,true,0x499,2)==5&&SP(0x4D05,true,0x49A,1)==5,"A slime casting Rupture outranks everything (Damage Down 18:23:57, 17:40:20)");
   Check(SP(0x4D03,false,0x49A,2)==0&&SP(0x4D04,false,0,0)==0,"Gigantis and Cyclops keep normal targeting");
   Check(SP(0x4D05,false,0,2)==3&&SP(0x4D07,false,0,2)==3,"No weapon element yet: any big slime");
   // Replay 22:39: small slimes never attacked, self-destructed -> Damage Down x8 (22:54:48) and x4 (22:55:22).
   Check(SP(0x4D08,false,0x49A,2)==2&&SP(0x4D06,false,0x499,0)==2,"Idle small slimes are attacked before Gigantis (priority 2)");
   Check(BossMod.Global.CrucibleOfTheUnbroken.SecondMasterTargetPolicy.Rupture(49376)&&BossMod.Global.CrucibleOfTheUnbroken.SecondMasterTargetPolicy.Rupture(49374)&&SP(0x4D08,true,0x49A,2)==4,"Small slime self-destruct cast raises it to 4");
   Check(SP(0x4D07,false,0x49A,2)>SP(0x4D08,false,0x49A,2)&&SP(0x4D07,true,0x49A,2)>SP(0x4D08,true,0x49A,2),"Spare big slime and Rupture outrank small slimes");
   // Replay 23:11: Lauda module no longer forces targets for BST, so RSR must rank the blades itself.
   Check(SP(0x4EB8,false,0,0)==3&&SP(0x4D22,false,0,0)==3,"Magicked / Indefatigable Blades priority 3 (LaudaTargetPolicy)");
   Check(SP(0x4D1E,false,0,0)==0&&BossMod.Global.CrucibleOfTheUnbroken.SecondMasterTargetPolicy.Tracked(0x4EB8)&&!BossMod.Global.CrucibleOfTheUnbroken.SecondMasterTargetPolicy.Tracked(0x4D1E),"Lauda herself keeps normal targeting");
   // Replay 2026-11-14 11:26: Durga module no longer forces a rook for BST; RSR ranks rooks, current first, then nearest.
   Check(SP(0x4CF8,false,0,0)==3&&BossMod.Global.CrucibleOfTheUnbroken.SecondMasterTargetPolicy.Tracked(0x4CF8),"Spinner-rook Pieces priority 3 (urgent filter)");
   Check(BossMod.Global.CrucibleOfTheUnbroken.SecondMasterTargetPolicy.PreferNearest(0x4CF8)&&!BossMod.Global.CrucibleOfTheUnbroken.SecondMasterTargetPolicy.PreferNearest(0x4EB8)&&!BossMod.Global.CrucibleOfTheUnbroken.SecondMasterTargetPolicy.PreferNearest(0x4D07),"Rooks rank nearest before lowest HP; blades and slimes keep lowest HP");
   Check(SP(0x4CF6,false,0,0)==0&&!BossMod.Global.CrucibleOfTheUnbroken.SecondMasterTargetPolicy.Tracked(0x4CF6),"Durga herself keeps normal targeting");
  }
  // Gluttonous Gutting hits Lauda's target (replays 22:39 player, 23:11 / 17:24 Behemoth): cover it with Snarl.
  Check(RotationSolver.Basic.Helpers.SecondMasterDefensePolicy.CoverCast(0x4D1E,49445),"Lauda Gluttonous Gutting requests cover");
  Check(!RotationSolver.Basic.Helpers.SecondMasterDefensePolicy.CoverCast(0x4D1E,49446),"Gutting helper cast is not the trigger (boss cast is)");
  Check(RotationSolver.Basic.Helpers.SecondMasterDefensePolicy.CoverCast(0x4D1E,49448)&&RotationSolver.Basic.Helpers.SecondMasterDefensePolicy.CoverCast(0x4D1E,49470),"Lauda Goring and Thunderbolt request cover (replay 23:42)");
  // Replay 23:42: First Master rules took cover back at player 77% / Coeurl 54%; Lauda keeps the familiar tanking.
  Check(!RotationSolver.Basic.Helpers.SecondMasterDefensePolicy.LaudaTakeBack(0.77f,0.54f)&&RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.TakeBack(0.77f,0.54f),"Lauda: no take-back at 77%/54% (First Master rule unchanged)");
  Check(RotationSolver.Basic.Helpers.SecondMasterDefensePolicy.LaudaTakeBack(0.84f,0.22f),"Lauda: take back when the familiar is nearly dead (84%/22%)");
  Check(!RotationSolver.Basic.Helpers.SecondMasterDefensePolicy.LaudaTakeBack(0.45f,0.2f),"Lauda: a low player never takes damage back");
  Check(RotationSolver.Basic.Helpers.SecondMasterDefensePolicy.LaudaCover(0.85f,0.6f)&&!RotationSolver.Basic.Helpers.SecondMasterDefensePolicy.LaudaCover(0.85f,0.3f),"Lauda: familiar covers whenever it has >=35% HP");
  Check(!(RotationSolver.Basic.Helpers.SecondMasterDefensePolicy.LaudaCover(0.6f,0.3f)&&RotationSolver.Basic.Helpers.SecondMasterDefensePolicy.LaudaTakeBack(0.6f,0.3f))&&!(RotationSolver.Basic.Helpers.SecondMasterDefensePolicy.LaudaCover(0.9f,0.25f)&&RotationSolver.Basic.Helpers.SecondMasterDefensePolicy.LaudaTakeBack(0.9f,0.25f)),"Lauda cover and take-back never both true (no ping-pong)");
 }
 Check(BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CCF)>BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CD1,true),"Growing Sapling remains highest priority even during Biloko heal");
 Check(BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CCF)>BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CD1),"Sapling outranks idle Biloko");
 Check(BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CCE)>BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CCD),"Slugs must be cleared before returning to Treant");
 Check(BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CD0)>BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CCE),"Diremite preempts remaining Slugs");
 Check(BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CD8)==0,"Treant policy does not classify unrelated bosses");
 var meteor=new RotationSolver.Basic.Helpers.TreantMeteorPolicy();
 var meteorStart=new DateTime(2026,11,10,22,0,0,DateTimeKind.Utc);
 Check(!meteor.Update(1,meteorStart,true,false,false,0,0,false).Summon,"Treant holds ready Behemoth before wave");
 Check(!meteor.Update(1,meteorStart.AddSeconds(20),true,false,false,6,0,false).Summon,"Distant newly spawned Slugs do not trigger summon");
 Check(meteor.Update(1,meteorStart.AddSeconds(22),true,false,false,6,4,false).Summon,"Nearby Slug cluster releases Behemoth reserve");
 Check(meteor.Update(1,meteorStart.AddSeconds(23),false,true,true,6,4,false).Release,"Meteor allowed against gathered adds");
 Check(meteor.Update(1,meteorStart.AddSeconds(24),false,true,false,6,4,false).HoldDismiss,"Do not dismiss while Meteor executes");
 Check(!meteor.Update(1,meteorStart.AddSeconds(29),false,true,false,1,1,false).HoldDismiss,"Execution hold expires");
 var timeoutMeteor=new RotationSolver.Basic.Helpers.TreantMeteorPolicy();
 timeoutMeteor.Update(2,meteorStart,true,false,false,0,0,false);
 Check(timeoutMeteor.Update(2,meteorStart.AddSeconds(31),true,false,false,0,0,false).Summon,"No-wave timeout prevents permanent reservation");
 Check(!timeoutMeteor.Update(3,meteorStart.AddSeconds(32),true,false,false,0,0,false).Summon,"New boss resets previous reserve state");
 Check(timeoutMeteor.Update(3,meteorStart.AddSeconds(33),true,false,false,0,0,true).Summon,"Emergency HP releases reserve");
 Check(!timeoutMeteor.Update(0,meteorStart.AddSeconds(34),true,false,false,6,6,false).Summon,"Outside encounter cannot request reserved summon");
 var sparseMeteor=new RotationSolver.Basic.Helpers.TreantMeteorPolicy();
 Check(!sparseMeteor.Update(4,meteorStart,true,false,false,2,1,false).Summon,"Sparse wave gets time to gather");
 Check(sparseMeteor.Update(4,meteorStart.AddSeconds(6),true,false,false,2,1,false).Summon,"Sparse surviving wave releases reserve after five seconds");
 var preSummonedMeteor=new RotationSolver.Basic.Helpers.TreantMeteorPolicy();
 Check(!preSummonedMeteor.Update(5,meteorStart,false,true,true,0,0,false).Release,"Pre-summoned Behemoth waits briefly for wave");
 Check(preSummonedMeteor.Update(5,meteorStart.AddSeconds(7),false,true,true,0,0,false).Release,"Active Behemoth release cannot wait forever");
 var masterXpConfig=new Configuration {AutoRecoveryItemHpThreshold=47,AutoOffensiveItemsEnabled=false,FirstBoardScoreEnabled=true,AutoEnterCrucibleEnabled=true,AutoCrucibleShopBuyEnabled=true};
 FirstMasterLegendaryProfile.ApplyXpRun(masterXpConfig);
 Check(FirstMasterLegendaryProfile.Active(masterXpConfig)&&FirstMasterLegendaryProfile.XpRun(masterXpConfig),"First Master XP retains combat integration signal");
 Check(!FirstBoardScorePolicy.XpRun(masterXpConfig)&&!masterXpConfig.FirstBoardScoreEnabled,"First Master XP does not activate Third Board or score mode");
 Check(masterXpConfig.SelectedCrucibleBoard=="特一盤"&&masterXpConfig.ClassMinMaxRankScanEnabled&&masterXpConfig.AutoCrucibleWhistleAssignEnabled,"Master XP selects board and enables rank/whistle preparation");
 Check(masterXpConfig.AutoRecoveryItemHpThreshold==47&&!masterXpConfig.AutoOffensiveItemsEnabled,"Master XP preserves current recovery and attack-item choices");
 Check(!masterXpConfig.AutoEnterCrucibleEnabled&&!masterXpConfig.AutoCrucibleShopBuyEnabled,"Master XP keeps menus manual");
 FirstMasterLegendaryProfile.Apply(masterXpConfig);
 Check(!FirstMasterLegendaryProfile.XpRun(masterXpConfig)&&FirstMasterLegendaryProfile.Active(masterXpConfig),"Can return to separate three-beast Legendary profile");
 var secondXpConfig=new Configuration {FirstBoardScoreEnabled=true,AutoEnterCrucibleEnabled=true,AutoCrucibleShopBuyEnabled=true};
 SecondMasterLegendaryProfile.ApplyXpRun(secondXpConfig);
 Check(SecondMasterLegendaryProfile.Active(secondXpConfig)&&SecondMasterLegendaryProfile.XpRun(secondXpConfig)&&SecondMasterLegendaryProfile.MasterTrioActive(secondXpConfig),"Second Master XP keeps the Second Master combat signal and trio whistles");
 Check(!FirstMasterLegendaryProfile.Active(secondXpConfig)&&!FirstBoardScorePolicy.XpRun(secondXpConfig)&&!secondXpConfig.FirstBoardScoreEnabled,"Second Master XP does not activate First Master, Third Board or score mode");
 Check(secondXpConfig.SelectedCrucibleBoard=="特二盤"&&secondXpConfig.AutoRecoveryItemHpThreshold==55&&secondXpConfig.FamiliarRecoveryHpThreshold==45&&secondXpConfig.AutoOffensiveItemsEnabled&&secondXpConfig.ClassMinMaxRankScanEnabled,"Second Master XP uses the Legendary item settings and rank scan");
 Check(!secondXpConfig.AutoEnterCrucibleEnabled&&!secondXpConfig.AutoCrucibleShopBuyEnabled,"Second Master XP keeps menus manual");
 SecondMasterLegendaryProfile.Apply(secondXpConfig);
 Check(!SecondMasterLegendaryProfile.XpRun(secondXpConfig)&&SecondMasterLegendaryProfile.Active(secondXpConfig),"Can return to the three-beast Second Master Legendary profile");
 Check(BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.Priority(0x4CBF)>BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.Priority(0x4CBD),"Queen Hawk is urgent over Corpse Flower");
 Check(RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.CoverCast(0x4CCD,50518),"Acorn Bomb requests cover before Nightmare");
 Check(RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.CoverCast(0x4CBF,48689),"Final Sting has cover fallback");
 Check(!RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.CoverCast(0x4CCD,48777),"Dodgeable Rustling Breeze is not replaced by pet cover");
 Check(RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.Cover(.65f,.95f),"Healthy familiar shares damage before player becomes critical");
 Check(!RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.Cover(.7f,.2f),"Do not sacrifice depleted pet for healthy player");
 Check(RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.TakeBack(.6f,.25f),"Take damage back before healthy player loses familiar");
 Check(!RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.TakeBack(.2f,.15f),"Critical player does not take lethal damage back");
 Check(!RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.CanCoverCast(.6f,.2f),"Avoid covering heavy cast with depleted familiar");
 Check(FamiliarRecoveryPolicy.SupportedPet(0x4A15,true,false),"First Master familiar recovery includes Behemoth");
 Check(!FamiliarRecoveryPolicy.SupportedPet(0x49FD,true,false),"First Master healing does not choose Buffalo");
 Check(FamiliarRecoveryPolicy.SupportedPet(0x49FD,false,true),"Third Board familiar recovery retains Buffalo");
 Check(!FamiliarRecoveryPolicy.SupportedPet(0x4A15,false,false),"No familiar auto-healing outside supported mode");
 Check(master.AutoFamiliarRecoveryEnabled&&master.FamiliarRecoveryHpThreshold==45,"First Master preset enables familiar recovery at 45 percent");
 // Borgny Toxic Mass reservation (RSR 7.5.7.138), timings from replay 2026-11-11 21:44 (Phlegm 00:11.75, spawn 00:21.33, first hit 00:26.7).
 var mass=new RotationSolver.Basic.Helpers.BorgnyToxicMassPolicy();
 var b0=new DateTime(2026,11,12,5,58,52,DateTimeKind.Utc);
 Check(!mass.Update(7,b0,false,false,false,false).Reserve,"Borgny opener is not reserved");
 Check(mass.Update(7,b0.AddSeconds(80),true,false,false,false).Reserve,"Wriggling Phlegm starts the Toxic Mass reservation");
 Check(mass.Update(7,b0.AddSeconds(89),false,true,false,false).Reserve,"Spawned but not yet targeted mass keeps the reservation");
 var burst=mass.Update(7,b0.AddSeconds(95),false,true,true,false);
 Check(burst.Burst&&!burst.Reserve,"Targeted mass releases summons and release onto it");
 Check(!RotationSolver.Basic.Helpers.BorgnyToxicMassPolicy.MassTargeted(true,true,false),"Mass burst waits while the familiar is still on Borgny (15:46 run: release hit Borgny)");
 Check(RotationSolver.Basic.Helpers.BorgnyToxicMassPolicy.MassTargeted(true,true,true),"Mass burst once player and familiar are both on the mass");
 Check(RotationSolver.Basic.Helpers.BorgnyToxicMassPolicy.MassTargeted(true,false,false),"No familiar out: mass targeted lets the summon opener go");
 Check(!RotationSolver.Basic.Helpers.BorgnyToxicMassPolicy.MassTargeted(false,true,true),"Player not on the mass: no burst");
 Check(!mass.Update(7,b0.AddSeconds(110),false,false,false,false).Reserve,"Dead or exploded mass ends the hold");
 Check(mass.Update(7,b0.AddSeconds(190),true,false,false,false).Reserve&&!mass.Update(7,b0.AddSeconds(208),false,false,false,false).Reserve,"Reservation is bounded at 17 seconds without a mass");
 Check(mass.Update(7,b0.AddSeconds(300),true,false,false,false).Reserve&&!mass.Update(7,b0.AddSeconds(301),false,false,false,true).Reserve,"Low player HP releases the reservation");
 Check(mass.Update(7,b0.AddSeconds(400),false,true,false,false).Reserve&&!mass.Update(7,b0.AddSeconds(413),false,true,false,false).Reserve,"Untargeted living mass hold is bounded at 12 seconds");
 Check(!mass.Update(0,b0.AddSeconds(500),true,true,false,false).Reserve,"No reservation outside Borgny");
 Check(mass.Update(8,b0.AddSeconds(600),true,false,false,false).Reserve&&!new RotationSolver.Basic.Helpers.BorgnyToxicMassPolicy().Update(9,b0,false,false,false,false).Reserve,"New encounter starts clean");
 // Item timing (BeastHelper 0.2.34).
 var h0=new DateTime(2026,11,12,5,53,6,DateTimeKind.Utc);
 Check(CrucibleItemTimingPolicy.HealStillPending(h0.AddSeconds(2),h0.AddSeconds(4),3900,3800,52f),"Second heal blocked while first has not landed (Gargoyle 21:53:06/08)");
 Check(!CrucibleItemTimingPolicy.HealStillPending(h0.AddSeconds(3),h0.AddSeconds(4),3900,6400,87f),"Lock clears once HP rises");
 Check(!CrucibleItemTimingPolicy.HealStillPending(h0.AddSeconds(4.1),h0.AddSeconds(4),3900,3800,52f),"Lock expires after four seconds");
 Check(!CrucibleItemTimingPolicy.HealStillPending(h0.AddSeconds(2),h0.AddSeconds(4),3900,1500,20f),"Critical HP bypasses the lock");
 Check(!CrucibleItemTimingPolicy.HealStillPending(h0,default,0,3800,52f),"No lock before any item use");
 Check(CrucibleItemTimingPolicy.AllowOffensive(false,1342,true,0x4CD8,1),"Offensive reservation only in First Master profile");
 Check(CrucibleItemTimingPolicy.AllowOffensive(true,1341,false,0x4C93,1),"Other boards unchanged");
 Check(!CrucibleItemTimingPolicy.AllowOffensive(true,1342,false,0x4CC2,1),"Last fang kept for Borgny");
 Check(CrucibleItemTimingPolicy.AllowOffensive(true,1342,false,0x4CC2,2),"Extra fangs still used on earlier bosses");
 Check(!CrucibleItemTimingPolicy.AllowOffensive(true,1342,true,0x4CD8,3),"No fangs on Borgny himself");
 Check(CrucibleItemTimingPolicy.AllowOffensive(true,1342,true,0x4CD9,1),"Fangs used on Toxic Mass");
 // 2026-11-12 22:01 run: Treant finish, Toxic Breath timing, Poison Cloud heading.
 Check(BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CCD,false,1996f/96115f)>BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CD1,false,1996f/96115f),"Treant at 2.1% outranks a fresh Biloko (22:15:24)");
 Check(BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CCD,false,0.05f)>BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CD1,true,0.05f),"Low Treant outranks a healing Biloko");
 Check(BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CCD,false,0.05f)>BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CCF,false,0.05f),"Low Treant outranks a Sapling");
 Check(BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CD1,false,25588f/96115f)>BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CCD,false,25588f/96115f),"Biloko still first while Treant is healthy (21:44, 26.6%)");
 Check(BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CCD,false,17187f/96115f)>BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CD1,false,17187f/96115f),"Treant at 17.9% outranks Biloko (22:49: Biloko-first cost ~20s)");
 Check(BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CCE,false,0.5f)>BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CCD,false,0.5f),"Slugs still first above the finish threshold");
 Check(!BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantFinishing(0f),"Unknown / zero HP does not trigger finish mode");
 Check(BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CD1,false,0.05f)==BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.TreantPriority(0x4CD1),"Finish mode leaves add priorities unchanged");
 Check(Math.Abs(2.7+BossMod.Global.CrucibleOfTheUnbroken.BorgnyAoePolicy.ToxicBreathFirstHitAfterCastEnd-5.33)<0.05,"Breath activation = earliest observed first hit (cast start +5.33s)");
 { var h=BossMod.Global.CrucibleOfTheUnbroken.BorgnyAoePolicy.CloudHeading(920f,-432f,920f,-432f,-0.79f); Check(Math.Abs(h-2.35)<0.02,"Fresh cloud uses reversed spawn facing (-0.79 -> +2.35)"); }
 { var h=BossMod.Global.CrucibleOfTheUnbroken.BorgnyAoePolicy.CloudHeading(920f,-432f,923.2f,-435.2f,-0.74f); Check(Math.Abs(h-2.36)<0.02,"Moving cloud uses travel direction (22:19:58 cloud)"); }
 { var h=BossMod.Global.CrucibleOfTheUnbroken.BorgnyAoePolicy.CloudHeading(928.5f,-411.5f,928.5f,-411.5f,0f); Check(Math.Abs(Math.Abs(h)-Math.PI)<0.01,"Reversed facing wraps into [-pi, pi]"); }
 {
   Check(Math.Abs(BossMod.Global.CrucibleOfTheUnbroken.BorgnyAoePolicy.MassSecondsToExplode(6.4f)-4.4)<0.15,"08:26 mass: 6.4y clearance explodes ~4.5s after spawn");
   Check(Math.Abs(BossMod.Global.CrucibleOfTheUnbroken.BorgnyAoePolicy.MassSecondsToExplode(24.1f)-22.1)<0.5,"22:01 mass: 24.1y clearance explodes ~21.9s after spawn");
   Check(BossMod.Global.CrucibleOfTheUnbroken.BorgnyAoePolicy.MassSecondsToExplode(5f)==BossMod.Global.CrucibleOfTheUnbroken.BorgnyAoePolicy.MassIdleSeconds,"Mass already at a puddle explodes after its idle time");
   var r0=BossMod.Global.CrucibleOfTheUnbroken.BorgnyAoePolicy.PhlegmReachRadius(5.4);
   Check(r0>20f&&r0<25f,"At the lock-on icon ~23y is reachable before the lock");
   Check(BossMod.Global.CrucibleOfTheUnbroken.BorgnyAoePolicy.PhlegmReachRadius(0.5)==0f,"Nothing beyond the current spot is reachable in the last 0.8s");
   // 08:26 case: player at 14.2y clearance; 16.3y spot 7.3y away beats the 22.4y spot 33y away.
   var near=BossMod.Global.CrucibleOfTheUnbroken.BorgnyAoePolicy.PhlegmBaitScore(16.3f,7.3f,r0);
   var far=BossMod.Global.CrucibleOfTheUnbroken.BorgnyAoePolicy.PhlegmBaitScore(22.4f,33f,r0);
   var stay=BossMod.Global.CrucibleOfTheUnbroken.BorgnyAoePolicy.PhlegmBaitScore(14.2f,0f,r0);
   Check(near>far&&near>stay,"Phlegm bait: best reachable clearance wins over a better unreachable spot");
   Check(BossMod.Global.CrucibleOfTheUnbroken.BorgnyAoePolicy.PhlegmBaitScore(14.2f,0f,0f)>BossMod.Global.CrucibleOfTheUnbroken.BorgnyAoePolicy.PhlegmBaitScore(22.4f,10f,0f),"Out of time: stay rather than run for a far spot");
   Check(BossMod.Global.CrucibleOfTheUnbroken.BorgnyAoePolicy.PhlegmBaitScore(20f,2f,0f)>BossMod.Global.CrucibleOfTheUnbroken.BorgnyAoePolicy.PhlegmBaitScore(14.2f,10f,0f),"Unreachable cells still rank by clearance minus extra travel");
 }
 Check(CrucibleItemTimingPolicy.UseBorgnyAntipoison(true,1342,0x40007105,0),"G2 Antipoison serum used at Borgny's pull");
 Check(!CrucibleItemTimingPolicy.UseBorgnyAntipoison(true,1342,0x40007105,0x40007105),"Serum used only once per Borgny encounter");
 Check(!CrucibleItemTimingPolicy.UseBorgnyAntipoison(true,1342,0,0),"No serum before Borgny is up");
 Check(!CrucibleItemTimingPolicy.UseBorgnyAntipoison(false,1342,0x40007105,0)&&!CrucibleItemTimingPolicy.UseBorgnyAntipoison(true,1341,0x40007105,0),"Serum rule only in First Master profile / territory");
 Check(CrucibleItemTimingPolicy.ReserveForBorgny(87,true,1342,false)&&!CrucibleItemTimingPolicy.ReserveForBorgny(87,true,1342,true),"Ribbon healing keeps G2 Antipoison for Borgny, may use it during Borgny");
 Check(!CrucibleItemTimingPolicy.ReserveForBorgny(86,true,1342,false)&&!CrucibleItemTimingPolicy.ReserveForBorgny(87,false,1342,false),"Other serums / profiles unchanged");
 // 2026-11-12 22:49 run: Snarl cover for single-target hits, familiar potion surplus, unchecked self items.
 Check(RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.CoverCast(0x4CC2,48717),"Sweeping Evisceration requests cover (3/3 redirected to pet)");
 Check(RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.CoverCast(0x4CD8,48809)&&RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.CoverCast(0x4CD8,48822),"Toxic Vomit and Salivous Snap request cover");
 Check(!RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.CoverCast(0x4CD8,48807)&&!RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.CoverCast(0x4CD8,48817),"Dodgeable Toxic Breath / Wriggling Phlegm are not covered");
 Check(3.2f+RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.AfterCastHold(0x4CD8,48809)>12.9f,"Toxic Vomit cover lasts past the fourth hit (+12.86s max)");
 Check(7.6f+RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.AfterCastHold(0x4CC2,48717)>9.02f,"Sweeping cover lasts past its hit (+9.02s max)");
 Check(6.7f+RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.AfterCastHold(0x4CD8,48822)>6.98f,"Salivous Snap cover lasts past its hit (+6.98s)");
 Check(3.2f+RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.AfterCastHold(0x4CD8,48809)<15f,"Toxic Vomit hold stays inside Snarl's 15s Cover");
 Check(RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.CoverCast(0x4CB6,48669),"Strix Darkness requests cover (9/9 redirected to Coeurl)");
 Check(RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.CoverCast(0x4CC2,50695)&&RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.CoverCast(0x4CC2,48724),"Fivefold Fallout requests cover");
 Check(RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.CoverCast(0x4CD8,48815),"Borgny Touchdown requests cover");
 { var h=2.5f+RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.AfterCastHold(0x4CC2,50695); Check(h>12.0f&&h<15f,"Fivefold cover lasts past the knockback hit (+12.0s) inside Snarl's 15s"); }
 Check(!RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.CoverCast(0x4CC2,48726)&&!RotationSolver.Basic.Helpers.FirstMasterDefensePolicy.CoverCast(0x4CC2,48719),"Dodgeable Desolation / Rippling Evisceration are not covered");
 Check(!CrucibleItemTimingPolicy.FamiliarHealAllowed(true,1)&&CrucibleItemTimingPolicy.FamiliarHealAllowed(true,2),"First Master keeps the last recovery item for the player");
 Check(CrucibleItemTimingPolicy.FamiliarHealAllowed(false,0),"Other profiles unchanged");
 { var u0=new DateTime(2026,11,12,23,6,0); Check(CrucibleItemTimingPolicy.AllowUncheckedSelfItem(u0,default)&&!CrucibleItemTimingPolicy.AllowUncheckedSelfItem(u0.AddSeconds(10),u0)&&CrucibleItemTimingPolicy.AllowUncheckedSelfItem(u0.AddSeconds(20),u0),"Unchecked self item retried at most every 20s"); }
 // Board / run menu (CrucibleRunSetup): every offered run applies and reads back as itself.
foreach (var rb in CrucibleRunSetup.Boards)
    foreach (var rr in CrucibleRunSetup.Runs(rb))
    {
        var rc = new Configuration { AutomationStopped = false, CrucibleTeamMode = CrucibleTeamMode.FirstMasterXpRun, ScoreVilekinCombo = true };
        CrucibleRunSetup.Apply(rc, rb, rr);
        Check(CrucibleRunSetup.Detect(rc) == (rb, rr), $"Run menu round trip: {CrucibleRunSetup.Name(rb, rr)} read back as {CrucibleRunSetup.Detect(rc)}");
    }
var fmXp = new Configuration { AutomationStopped = false };
CrucibleRunSetup.Apply(fmXp, CrucibleBoard.FirstMaster, CrucibleRun.XpRun);
Check(FirstMasterLegendaryProfile.XpRun(fmXp) && fmXp.FirstMasterXpBoardMenusEnabled && fmXp.SelectedCrucibleBoard == "特一盤" && !fmXp.AutoEnterCrucibleEnabled,
    "First Master XP Run from the menu = the old First Master XP roster mode");
var smLeg = new Configuration { AutomationStopped = false };
CrucibleRunSetup.Apply(smLeg, CrucibleBoard.SecondMaster, CrucibleRun.Legendary);
Check(SecondMasterLegendaryProfile.Active(smLeg) && !SecondMasterLegendaryProfile.XpRun(smLeg) && smLeg.AutoRecoveryItemHpThreshold == 55, "Second Master Legendary from the menu = old preset");
var runKin = new Configuration { AutomationStopped = false };
CrucibleRunSetup.Apply(runKin, CrucibleBoard.ThirdBoard, CrucibleRun.BeastkinClear);
Check(FirstBoardScorePolicy.BeastkinClear(runKin) && runKin.ScoreBoardTerritory == 1341 && runKin.AutoRecoveryItemHpThreshold == 60f && runKin.AutoOffensiveItemsEnabled, "Third Board Beastkin Clear = old Prepare button");
var score = new Configuration { AutomationStopped = false, AutoOffensiveItemsEnabled = true, AutoEnterCrucibleEnabled = true };
CrucibleRunSetup.Apply(score, CrucibleBoard.FirstBoard, CrucibleRun.ScoreRun);
Check(score.FirstBoardScoreEnabled && !score.AutoOffensiveItemsEnabled && !score.AutoEnterCrucibleEnabled, "Score Run still enforces no items / no auto-entry");
var custom = new Configuration { AutomationStopped = false, CrucibleTeamMode = CrucibleTeamMode.SecondMasterXpRun, CombatOnlyEnabled = true };
Check(!CrucibleRunSetup.Apply(custom, CrucibleBoard.SecondMaster, CrucibleRun.Custom) && !SecondMasterLegendaryProfile.Active(custom) && custom.CrucibleTeamMode == CrucibleTeamMode.Leveling,
    "Custom leaves the named profiles and keeps a generic roster mode");
Console.WriteLine($"PASS: {count} First Board score restrictions and fixed-trio checks.");
}