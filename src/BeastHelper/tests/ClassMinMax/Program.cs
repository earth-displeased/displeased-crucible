using BeastNav.Models;
using BeastNav.Services;
static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
var rng = new Random(2026);
for (var run = 0; run < 500; run++)
{
    var pets = Enumerable.Range(1, 20).Select(i => new BeastPet
        { PetRowId = (uint)i, XbmRowId = (uint)i, Classification = (byte)rng.Next(1,9), Rank = 5 }).ToArray();
    var progress = pets.ToDictionary(p => p.PetRowId, _ => new BeastProgress { Level = rng.Next(1,26) });
    // Independent exhaustive oracle: enumerate every valid same-class triple.
    var all = new List<(int Sum, int Max, byte Class)>();
    for (var a=0; a<pets.Length; a++) for(var b=a+1; b<pets.Length; b++) for(var c=b+1;c<pets.Length;c++)
    {
        if (pets[a].Classification != pets[b].Classification || pets[b].Classification != pets[c].Classification) continue;
        var levels = new[] {progress[pets[a].PetRowId].Level, progress[pets[b].PetRowId].Level, progress[pets[c].PetRowId].Level};
        all.Add((levels.Sum(), levels.Max(), pets[a].Classification));
    }
    var selected = CrucibleTeamPlanner.ClassMinMax(pets, progress, out _);
    if (all.Count == 0) { Check(selected.Count == 0, "No valid triple should yield none"); continue; }
    var expected = all.Order().First();
    Check(selected.Count == 3 && selected.Select(p => p.Classification).Distinct().Count() == 1, "Exactly three of one classification");
    Check((selected.Sum(p => progress[p.PetRowId].Level), selected.Max(p => progress[p.PetRowId].Level), selected[0].Classification) == expected, "Global minimum / tie breaks differ from brute force");
    Check(CrucibleTeamPlanner.ClassMinMax(pets.Reverse(), progress, out _).Select(p=>p.PetRowId).SequenceEqual(selected.Select(p=>p.PetRowId)), "Unstable input-order tie break");
    Check(CrucibleTeamPlanner.ClassMinMax(pets.Concat(pets), progress, out _).Select(p=>p.PetRowId).SequenceEqual(selected.Select(p=>p.PetRowId)), "Duplicates changed selection");
    progress.Remove(pets[0].PetRowId);
    Check(CrucibleTeamPlanner.ClassMinMax(pets, progress, out var reason).Count == 0 && reason.Contains("Waiting"), "Unknown ranks must not masquerade as low ranks");
}
Check((int)CrucibleTeamMode.Leveling == 0 && (int)CrucibleTeamMode.Manual == 1 && (int)CrucibleTeamMode.Recommended == 2, "Existing saved mode IDs changed");
for(uint row=1;row<=50;row++)
    Check(BeastClassifications.ForReferenceRow(row,row+(row<=4?46u:47u),242000+row) is >=1 and <=8, "Reference classification missing");
Check(BeastClassifications.ForReferenceRow(1,999,242001)==0, "Identity guard missing");
Check(BeastClassifications.ForReferenceRow(51,98,242051)==0, "Unknown beast guessed");
Console.WriteLine("Passed 500 exhaustive-comparison scenarios, deterministic/duplicate checks, missing ranks, classification guards, and saved enum compatibility.");

var xpOwned=Enumerable.Range(1,50).Select(i=>new BeastPet {PetRowId=(uint)(100+i),XbmRowId=(uint)i,Name=$"Beast {i}",Rank=5}).ToArray();
var xpProgress=xpOwned.ToDictionary(p=>p.PetRowId,p=>new BeastProgress {Level=(int)(p.XbmRowId%25)+1});
var xp=CrucibleTeamPlanner.ThirdBoardXpRun(xpOwned,xpProgress,out var xpReason);
Check(xp.Count==14 && xpReason.Length==0,"XP roster fills all 14 places");
Check(xp.Take(3).Select(p=>p.XbmRowId).SequenceEqual(new uint[]{1,26,33}),"Core combat trio retained first regardless of levels");
var expectedXp=xpOwned.Where(p=>p.XbmRowId is not 1 and not 26 and not 33).OrderBy(p=>xpProgress[p.PetRowId].Level).ThenBy(p=>p.XbmRowId).Take(11).Select(p=>p.XbmRowId);
Check(xp.Skip(3).Select(p=>p.XbmRowId).SequenceEqual(expectedXp),"Remaining 11 are globally lowest actual ranks");
Check(CrucibleTeamPlanner.ThirdBoardXpRun(xpOwned.Reverse().Concat(xpOwned),xpProgress,out _).SequenceEqual(xp),"XP input order and duplicates cannot alter roster");
Check(CrucibleTeamPlanner.ThirdBoardXpRun(xpOwned.Where(p=>p.XbmRowId!=26),xpProgress,out var missingCore).Count==0 && missingCore.Contains("Buffalo"),"Missing core beast cannot be silently substituted");
xpProgress.Remove(102);
Check(CrucibleTeamPlanner.ThirdBoardXpRun(xpOwned,xpProgress,out var missingRank).Count==14 && missingRank.Length==0,"Unknown rank no longer pauses the Third Board XP roster");
Check(CrucibleTeamPlanner.ThirdBoardXpRun(xpOwned.Where(p=>p.XbmRowId is 1 or 26 or 33),xpProgress,out _).Count==3,"Small owned roster keeps available core without invented extras");
Check((int)CrucibleTeamMode.ClassMinMax==3 && (int)CrucibleTeamMode.ThirdBoardXpRun==4,"New mode preserves existing saved enum values");
Console.WriteLine("PASS: Third Board XP roster checks.");

var masterOwned=xpOwned;
var masterProgress=masterOwned.ToDictionary(p=>p.PetRowId,p=>new BeastProgress {Level=(int)(p.XbmRowId%25)+1});
var masterXp=CrucibleTeamPlanner.FirstMasterXpRun(masterOwned,masterProgress,out var masterReason);
Check(masterXp.Count==12 && masterReason.Length==0,"First Master XP fills 12 places");
Check(masterXp.Take(3).Select(p=>p.XbmRowId).SequenceEqual(new uint[]{1,50,33}),"First Master keeps successful combat trio");
var expectedMaster=masterOwned.Where(p=>p.XbmRowId is not 1 and not 50 and not 33).OrderBy(p=>masterProgress[p.PetRowId].Level).ThenBy(p=>p.XbmRowId).Take(9).Select(p=>p.XbmRowId);
Check(masterXp.Skip(3).Select(p=>p.XbmRowId).SequenceEqual(expectedMaster),"First Master extras are nine lowest actual ranks");
Check(CrucibleTeamPlanner.FirstMasterXpRun(masterOwned.Reverse().Concat(masterOwned),masterProgress,out _).SequenceEqual(masterXp),"First Master stable tie breaks and duplicate handling");
Check(CrucibleTeamPlanner.FirstMasterXpRun(masterOwned.Where(p=>p.XbmRowId!=50),masterProgress,out var missingMaster).Count==0 && missingMaster.Contains("Behemoth"),"No Buffalo substitution for missing Behemoth");
masterProgress.Remove(masterOwned.First(p=>p.XbmRowId==2).PetRowId);
Check(CrucibleTeamPlanner.FirstMasterXpRun(masterOwned,masterProgress,out var unknownMaster).Count==12 && unknownMaster.Length==0,"Unknown Master XP rank no longer pauses selection");
Check(CrucibleTeamPlanner.FirstMasterXpRun(masterOwned.Where(p=>p.XbmRowId is 1 or 50 or 33),masterProgress,out _).Count==3,"Small Master roster retains core only");
Check((int)CrucibleTeamMode.FirstMasterLegendary==5 && (int)CrucibleTeamMode.FirstMasterXpRun==6,"Saved Legendary and XP modes remain distinct");
var secondProgress=xpOwned.ToDictionary(p=>p.PetRowId,p=>new BeastProgress {Level=(int)(p.XbmRowId%25)+1});
var secondXp=CrucibleTeamPlanner.SecondMasterXpRun(xpOwned,secondProgress,out var secondReason);
var expectedSecondCount=Math.Min(15,xpOwned.DistinctBy(p=>p.PetRowId).Count());
Check(secondXp.Count==expectedSecondCount && secondReason.Length==0,"Second Master XP fills up to 15 places");
Check(secondXp.Take(3).Select(p=>p.XbmRowId).SequenceEqual(new uint[]{1,50,33}),"Second Master XP keeps the Legendary combat trio first");
var expectedSecond=xpOwned.Where(p=>p.XbmRowId is not 1 and not 50 and not 33).OrderBy(p=>secondProgress[p.PetRowId].Level).ThenBy(p=>p.XbmRowId).Take(12).Select(p=>p.XbmRowId);
Check(secondXp.Skip(3).Select(p=>p.XbmRowId).SequenceEqual(expectedSecond),"Second Master XP extras are the twelve lowest ranks");
Check(CrucibleTeamPlanner.SecondMasterXpRun(xpOwned.Where(p=>p.XbmRowId!=50),secondProgress,out var missingSecond).Count==0 && missingSecond.Contains("Behemoth"),"Second Master XP never substitutes a missing Behemoth");
Check((int)CrucibleTeamMode.SecondMasterLegendary==7 && (int)CrucibleTeamMode.SecondMasterXpRun==8,"Second Master XP mode appended; saved values unchanged");
// Unknown ranks (missing or Level 0) no longer block the Second Master XP roster: ordered as rank 1, after known rank 1.
var sparse=xpOwned.ToDictionary(p=>p.PetRowId,p=>new BeastProgress {Level=0});
sparse[xpOwned.First(p=>p.XbmRowId==2).PetRowId]=new BeastProgress {Level=25};
sparse[xpOwned.First(p=>p.XbmRowId==40).PetRowId]=new BeastProgress {Level=1};
sparse.Remove(xpOwned.First(p=>p.XbmRowId==3).PetRowId);
var sparseXp=CrucibleTeamPlanner.SecondMasterXpRun(xpOwned,sparse,out var sparseReason);
Check(sparseXp.Count==15 && sparseReason.Length==0,"Unknown ranks still fill all 15 places");
Check(sparseXp.Take(3).Select(p=>p.XbmRowId).SequenceEqual(new uint[]{1,50,33}),"Trio first with unknown ranks");
Check(sparseXp.Skip(3).Select(p=>p.XbmRowId).SequenceEqual(new uint[]{40,3,4,5,6,7,8,9,10,11,12,13}),"Known rank 1 first, then unknowns by number; rank 25 excluded");
Check(CrucibleTeamPlanner.FirstMasterXpRun(xpOwned,sparse,out var fmSparse).Select(p=>p.XbmRowId).SequenceEqual(new uint[]{1,50,33,40,3,4,5,6,7,8,9,10}) && fmSparse.Length==0,"First Master XP fills 12 with unknown ranks");
Check(CrucibleTeamPlanner.ThirdBoardXpRun(xpOwned,sparse,out _).Select(p=>p.XbmRowId).SequenceEqual(new uint[]{1,26,33,40,3,4,5,6,7,8,9,10,11,12}),"Third Board XP fills 14 with unknown ranks");
Console.WriteLine("First Master and Third Board XP roster checks passed.");

// Whistle-screen rank query (GetSyncedRank / IsPetOverSyncedRank).
Check(BeastRankReadPolicy.Resolve(7,false,null)==7 && BeastRankReadPolicy.Resolve(7,false,3)==7,"Synced rank at or below the cap is the real rank");
Check(BeastRankReadPolicy.Resolve(0,false,5)==null && BeastRankReadPolicy.Resolve(26,false,null)==null,"Invalid reads never overwrite");
Check(BeastRankReadPolicy.Resolve(20,true,null)==21 && BeastRankReadPolicy.Resolve(20,true,12)==21,"Over the cap saves cap+1 when nothing better is known");
Check(BeastRankReadPolicy.Resolve(20,true,24)==null,"Over the cap keeps a saved rank that is already above it");
Check(BeastRankReadPolicy.Resolve(25,true,null)==25,"Never above 25");
// Leveled passengers are replaced next entry: after a run the passengers' new ranks sort them behind fresher beasts.
var after=xpOwned.ToDictionary(p=>p.PetRowId,p=>new BeastProgress {Level=1});
foreach(var p in xpOwned.Where(p=>p.XbmRowId is >=2 and <=10)) after[p.PetRowId]=new BeastProgress {Level=3};
var nextRun=CrucibleTeamPlanner.FirstMasterXpRun(xpOwned,after,out _).Skip(3).Select(p=>p.XbmRowId).ToArray();
Check(nextRun.SequenceEqual(new uint[]{11,12,13,14,15,16,17,18,19}),"Leveled passengers swapped for the lowest-rank beasts");
Console.WriteLine("PASS: rank refresh checks.");

// Gear policy: mitigation first, then damage; harmful gear never chosen.
Check(CrucibleGearPolicy.Table.Count==75 && Enumerable.Range(1,75).All(r=>CrucibleGearPolicy.IsGear((uint)r)),"All 75 gear rows ranked");
Check(CrucibleGearPolicy.Best(new uint[]{50,51,9})==51,"Mythril Armor (mitigation) beats Heavy Axe (damage)");
Check(CrucibleGearPolicy.Best(new uint[]{24,21,51})==24,"Master Shield is the top mitigation");
Check(CrucibleGearPolicy.Best(new uint[]{50,7,19})==50,"Without mitigation gear, highest damage wins");
Check(CrucibleGearPolicy.Best(new uint[]{61,65,71,31})==null,"Self-harming or battle-skipping gear never chosen");
Check(CrucibleGearPolicy.Best(new uint[]{60,67,76})==null,"Gear too weak to matter and potions are not gear picks");
Check(CrucibleGearPolicy.Best(new uint[]{27,49})==49 && CrucibleGearPolicy.Best(new uint[]{27,13})==27,"Mitigation decides first, damage only breaks ties within the tier");
Check(CrucibleGearPolicy.FromCardText("Damage Dealt +30%\nThunder Axe")==61 && CrucibleGearPolicy.FromCardText("Thunderstruck Axe")==40,"Card text resolves to the right gear");
Check(CrucibleGearPolicy.FromCardText("G2 Beast Potion")==null,"Non-gear cards are not gear");
Console.WriteLine("PASS: gear policy checks.");

// English shop values (windowdump 2026-11-14 18:23) and the English leave prompt.
Check(CrucibleConfirmationText.ParseShownNumber("1,500")==1500 && CrucibleConfirmationText.ParseShownNumber("52 (-50%)")==52 && CrucibleConfirmationText.ParseShownNumber("840")==840,"Shop coins and prices shown as text");
Check(CrucibleConfirmationText.ParseShownNumber("?")==0 && CrucibleConfirmationText.ParseShownNumber("")==0,"Unreadable values are 0 (never affordable as coins)");
Check(CrucibleConfirmationText.IsShopAction("Conclude purchasing and leave the shop?\nYou cannot return to the shop after leaving."),"English leave prompt recognized");
Check(!CrucibleConfirmationText.IsShopAction("Abandon the duty?") ,"Unrelated prompt not treated as shop");
Console.WriteLine("PASS: shop text checks.");
var sorted=Enumerable.Range(1,50).Select(n=>(uint)(51-n)).ToList(); // bestiary sorted in reverse
Check(NotebookSlotPolicy.Locate(sorted,50)==(0,0) && NotebookSlotPolicy.Locate(sorted,1)==(1,24),"Sorted bestiary: slot comes from the game's list, not the beast number");
Check(NotebookSlotPolicy.Locate(new List<uint>{3,7,9},8)==null,"Beast hidden by a filter: no click");
Check(NotebookSlotPolicy.Locate(new List<uint>(),27)==(1,1),"List not loaded: number-based fallback");
Console.WriteLine("PASS: bestiary slot checks.");
