using System.Text;

namespace DNESaver
{
    public class FactTimeline
    {
        public required FactDefinition Fact;
        public required Dictionary<string, FactValue> ValueTimeline;

        public string FirstScene { get => ValueTimeline.Keys.FirstOrDefault() ?? ""; }

        public string GetHistoryString(bool includeHeader = true)
        {
            var sb = new StringBuilder();
            if (includeHeader)
            {
                if (!String.IsNullOrWhiteSpace(Fact.FactName))
                {
                    sb.AppendLine($"Fact {Fact.FactAssetName} / {Fact.FactName} -> {Fact.Type}");
                }
                else
                {
                    sb.AppendLine($"Fact {Fact.FactAssetId} / {Fact.FactId} -> {Fact.Type}");
                }
            }

            FactValue? LastValue = null;
            foreach (var tl in ValueTimeline)
            {
                var currentValue = tl.Value;

                string GetEnumValue()
                {
                    var thevalue = Fact.EnumValues?.FirstOrDefault(a => a.Numerical == currentValue.EnumValue);
                    if (thevalue != null) return $"{thevalue.Name} [{thevalue.Numerical}]";
                    return currentValue.EnumValue.ToString();
                }

                string ValueString = Fact.Type switch
                {
                    FactType.EnumFact => GetEnumValue(),
                    FactType.IntFact => currentValue.IntValue.ToString(),
                    FactType.FloatFact => currentValue.FloatValue.ToString(),
                    FactType.BoolFact => currentValue.BoolValue.ToString(),
                    _ => ""
                };

                string LastSceneName = "(before game)";
                int idx = SavegameModifier.SceneNameMappings.Keys.ToList().IndexOf(tl.Key) - 1;
                if (tl.Key == "S6200_10" || tl.Key == "S6300_10" || tl.Key == "S6400_10" || tl.Key == "S6500_10")
                {
                    LastSceneName = "2-17 Time Capsule";
                }
                else if (idx >= 0)
                {
                    LastSceneName = SavegameModifier.SceneNameMappings[SavegameModifier.SceneNameMappings.Keys.ToList()[idx]];
                }
                else if (tl.Key == "global")
                {
                    LastSceneName = "Global Scene";
                }

                if (LastValue == null)
                {
                    sb.AppendLine($"\tFirst Set in Scene {LastSceneName} -> {ValueString}");
                }
                else
                {
                    if (LastValue.Value.IntValue != currentValue.IntValue
                        || LastValue.Value.BoolValue != currentValue.BoolValue
                        || LastValue.Value.FloatValue != currentValue.FloatValue
                        || LastValue.Value.EnumValue != currentValue.EnumValue)
                    {
                        sb.AppendLine($"\tModified in Scene {LastSceneName} -> {ValueString}");
                    }
                }

                LastValue = currentValue;
            }
            return sb.ToString();
        }
    }

    public class Relationship
    {
        public string name { get; set; }
        public byte level { get; set; }
        public byte growth { get; set; }
        public byte decay { get; set; }
        public int growthChanges { get; set; }
        public int decayChanges { get; set; }
    }

    public class SavegameModifier
    {
        public static readonly Dictionary<string, string> SceneNameMappings = new() {
    {"S1000_10", "1-1 Expectations"},
    {"S1100_10", "1-2 Memory Lane"},
    {"S1200_10", "1-3 Swann Holloway, 1995"},
    {"S1250_10", "1-4 Welcome to Velvet Cove"},
    {"S1400_10", "1-5 The Day we Met"},
    {"S1500_10", "1-6 The first Night"},
    {"S1600_10", "1-7 Autumn Lockhart, 1995"},
    {"S2000_10", "1-8 Reunion"},
    {"S2300_05", "1-9 Garage Band"},
    {"S2500_00", "1-10 Lights, Camera, Action"},
    {"S2600_30", "1-11 Movie Night"},
    {"S2700_00", "1-12 Don't be afraid of the Dark"},
    {"S2710_04", "1-13 Cabin in the Woods"},
    {"S2720_04", "1-14 Phone a Friend"},
    {"S2740_10", "1-15 This old House"},
    {"S2740_40", "1-16 Echos of Summer"},
    {"S2760_10", "1-17 Nora Malakian, 1995"},
    {"S2770_10", "1-18 Bloom..."},
    {"S2800_10", "1-19 Packing Up"},
    {"S2860_30", "1-20 Double Dare"},
    {"S2870_10", "1-21 The Abyss"},
    {"S3000_10", "1-22 Doubts"},
    {"S3100_10", "1-23 Kat(hryn) Mikaelsen, 1995"},
    {"S3200_10", "1-24 Riot Grrrls"},
    {"S3400_10", "1-25 ...& Rage"},

    //Tape 2 

    {"S4000_05", "2-1 Nightmare"},
    {"S4000_20", "2-2 The Lock"},
    {"S4100_10", "2-3 Alone Again"},
    {"S4200_10", "2-4 A Tale of Two Sisters"},
    {"S4300_10", "2-5 An Empty Cabin"},
    {"S4350_10", "2-6 Pieces of Autumn"},
    {"S4450_10", "2-7 Nora's Grief"},
    {"S4700_10", "2-8 Infiltration"},
    {"S4800_10", "2-9 Rapunzel"},
    {"S4900_10", "2-10 Wishing on a Star"},
    {"S5000_10", "2-11 The Gathering"},
    {"S5200_10", "2-12 Anarchist's Dream"},
    {"S5300_10", "2-13 Hunted"},
    {"S5400_10", "2-14 The Coven"},
    {"S5500_10", "2-15 The Wolf Among Us"},
    {"S5600_10", "2-16 Enter the Void "},
    {"S6100_10", "2-17 Time Capsule "},
    {"S6200_10", "2-18 Remember Us"},       // Ending 1
    {"S6300_10", "2-18 Remember Us"},       // Ending 2
    {"S6400_10", "2-18 Remember Us"},       // Ending 3
    {"S6500_10", "2-18 Remember Us"},       // Ending 4
    {"S6600_10", "2-19 Lost Records"},      // Post Creds
};

        private readonly DNESaveFile savefile;
        private readonly SaveFilePatcher patcher;

        public SavegameModifier(DNESaveFile savefile, SaveFilePatcher patcher)
        {
            this.savefile = savefile;
            this.patcher = patcher;
        }

        public void PrintRelationshipDump()
        {
            DNESaveFile.SavProperty SceneSnapshots = savefile["SceneSnapshots"] ?? throw new Exception("SceneSnapshots not found.");
            foreach (var scene in SceneSnapshots.Children())
            {
                if (scene == null) continue;

                string sceneId = (scene["Section"] as DNESaveFile.StrSavProperty)?.Value ?? "";
                string sceneName = SavegameModifier.SceneNameMappings[sceneId];

                Console.WriteLine($"Scene: {sceneName} ({sceneId})");

                var relationships = scene["CharactersRelationships"];

                if (relationships == null)
                {
                    Console.WriteLine("No relationships?");
                    continue;
                }

                foreach (var relationship in relationships.Children())
                {
                    var name = relationship["RelationshipName"] as DNESaveFile.StrSavProperty ?? throw new Exception("required field missing"); ;
                    var level = relationship["Level"] as DNESaveFile.NumericSavProperty<byte> ?? throw new Exception("required field missing");
                    var growth = relationship["GrowthValue"] as DNESaveFile.NumericSavProperty<byte> ?? throw new Exception("required field missing");
                    var decay = relationship["DecayValue"] as DNESaveFile.NumericSavProperty<byte> ?? throw new Exception("required field missing");
                    var growthChanges = relationship["GrowthChangesCount"] as DNESaveFile.NumericSavProperty<int> ?? throw new Exception("required field missing");
                    var decayChanges = relationship["DecayChangesCount"] as DNESaveFile.NumericSavProperty<int> ?? throw new Exception("required field missing");
                    Console.WriteLine($"\t{name.Value,-37}: level {level:a} (growth: {growth:a} [{growthChanges:a}x]; decay: {decay:a} [{decayChanges:a}x])");
                }
                Console.WriteLine();
            }
        }

        public Dictionary<string, Relationship[]> GetRelationships()
        {
            IEnumerable<Relationship> RelationshipsFromScene(DNESaveFile.SavProperty scene)
            {
                var relationships = scene["CharactersRelationships"];
                if (relationships == null) yield break;

                foreach (var relationship in relationships.Children())
                {
                    var name = relationship["RelationshipName"] as DNESaveFile.StrSavProperty ?? throw new Exception("required field missing"); ;
                    var level = relationship["Level"] as DNESaveFile.NumericSavProperty<byte> ?? throw new Exception("required field missing");
                    var growth = relationship["GrowthValue"] as DNESaveFile.NumericSavProperty<byte> ?? throw new Exception("required field missing");
                    var decay = relationship["DecayValue"] as DNESaveFile.NumericSavProperty<byte> ?? throw new Exception("required field missing");
                    var growthChanges = relationship["GrowthChangesCount"] as DNESaveFile.NumericSavProperty<int> ?? throw new Exception("required field missing");
                    var decayChanges = relationship["DecayChangesCount"] as DNESaveFile.NumericSavProperty<int> ?? throw new Exception("required field missing");
                    yield return new Relationship() { name = name.Value, level = level.Value, decay = decay.Value, decayChanges = decayChanges.Value, growth = growth.Value, growthChanges = growthChanges.Value };
                }
            }

            Dictionary<string, Relationship[]> result = [];

            DNESaveFile.SavProperty SceneSnapshots = savefile["SceneSnapshots"] ?? throw new Exception("SceneSnapshots not found.");
            foreach (var scene in SceneSnapshots.Children())
            {
                if (scene == null) continue;

                string sceneId = (scene["Section"] as DNESaveFile.StrSavProperty)?.Value ?? "";
                result[sceneId] = RelationshipsFromScene(scene).ToArray();
            }

            var currentSnapshot = savefile["CurrentSnapshot"] ?? throw new Exception("CurrentSnapshot not found.");
            result["global"] = RelationshipsFromScene(currentSnapshot).ToArray();

            return result;
        }

        private bool ModifyFact(DNESaveFile.SavProperty Scene, FactDefinition fact, FactValue newValue)
        {
            var factsStruct = Scene["Facts"] ?? throw new Exception("Facts missing");
            var FactAssetsMap = factsStruct["FactAssets"] ?? throw new Exception("FactAssets missing");
            var factlist = FactAssetsMap[fact.FactAssetId];
            if (factlist == null) return false;

            DNESaveFile.SavProperty? ListProperty;
            switch (fact.Type)
            {
                case FactType.BoolFact:
                    ListProperty = factlist["BoolFacts"];
                    if (ListProperty == null || ListProperty is not DNESaveFile.MapSavProperty<bool> boolfacts) return false;
                    if (!boolfacts.Map.ContainsKey(fact.FactId)) return false;
                    patcher.Patch<bool>(boolfacts, fact.FactId, newValue.BoolValue);
                    break;
                case FactType.IntFact:
                    ListProperty = factlist["IntFacts"];
                    if (ListProperty == null || ListProperty is not DNESaveFile.MapSavProperty<int> intfacts) return false;
                    if (!intfacts.Map.ContainsKey(fact.FactId)) return false;
                    patcher.Patch<int>(intfacts, fact.FactId, newValue.IntValue);

                    break;
                case FactType.FloatFact:
                    ListProperty = factlist["FloatFacts"];
                    if (ListProperty == null || ListProperty is not DNESaveFile.MapSavProperty<float> floatfacts) return false;
                    if (!floatfacts.Map.ContainsKey(fact.FactId)) return false;
                    patcher.Patch<float>(floatfacts, fact.FactId, newValue.FloatValue);
                    break;
                case FactType.EnumFact:
                    ListProperty = factlist["EnumFacts"];
                    if (ListProperty == null || ListProperty is not DNESaveFile.MapSavProperty<byte> enumfacts) return false;
                    if (!enumfacts.Map.ContainsKey(fact.FactId)) return false;
                    patcher.Patch<byte>(enumfacts, fact.FactId, newValue.EnumValue);
                    break;
                default:
                    return false;
            }

            return true;
        }

        public bool AdjustFact(FactDefinition fact, FactValue value, IEnumerable<string> Scenes, bool AlterGlobalSave)
        {
            DNESaveFile.SavProperty SceneSnapshots = savefile["SceneSnapshots"] ?? throw new Exception("SceneSnapshots not found.");
            bool success = true;
            foreach (var SceneId in Scenes)
            {
                var scene = SceneSnapshots[SceneId];
                if (scene == null)
                {
                    Console.WriteLine($"Warning: Scene {SceneId} not found.");
                    continue;
                }

                success &= ModifyFact(scene, fact, value);
            }

            if (AlterGlobalSave)
            {
                var currentSnapshot = savefile["CurrentSnapshot"] ?? throw new Exception("CurrentSnapshot not found.");
                success &= ModifyFact(currentSnapshot, fact, value);
            }
            return success;
        }

        private void ModifySave(DNESaveFile.SavProperty Scene, Relationship rel)
        {
            var relationships = Scene["CharactersRelationships"];
            var rel_to_fix = relationships[rel.name];

            var fix_level = rel_to_fix["Level"] as DNESaveFile.NumericSavProperty<byte> ?? throw new Exception("required field missing");
            var fix_growth = rel_to_fix["GrowthValue"] as DNESaveFile.NumericSavProperty<byte> ?? throw new Exception("required field missing");
            var fix_decay = rel_to_fix["DecayValue"] as DNESaveFile.NumericSavProperty<byte> ?? throw new Exception("required field missing");
            var fix_growthChanges = rel_to_fix["GrowthChangesCount"] as DNESaveFile.NumericSavProperty<int> ?? throw new Exception("required field missing");
            var fix_decayChanges = rel_to_fix["DecayChangesCount"] as DNESaveFile.NumericSavProperty<int> ?? throw new Exception("required field missing");

            patcher.Patch<byte>(fix_level, rel.level);
            patcher.Patch<byte>(fix_growth, rel.growth);
            patcher.Patch<byte>(fix_decay, rel.decay);
            patcher.Patch<int>(fix_growthChanges, rel.growthChanges);
            patcher.Patch<int>(fix_decayChanges, rel.decayChanges);
        }

        public void AdjustRelationship(Relationship rel, IEnumerable<string> Scenes, bool AlterGlobalSave)
        {
            DNESaveFile.SavProperty SceneSnapshots = savefile["SceneSnapshots"] ?? throw new Exception("SceneSnapshots not found.");
            foreach (var SceneId in Scenes)
            {
                var scene = SceneSnapshots[SceneId];
                if (scene == null)
                {
                    Console.WriteLine($"Warning: Scene {SceneId} not found.");
                    continue;
                }

                ModifySave(scene, rel);
            }

            if (AlterGlobalSave)
            {
                var currentSnapshot = savefile["CurrentSnapshot"] ?? throw new Exception("CurrentSnapshot not found.");
                ModifySave(currentSnapshot, rel);
            }
        }

        private IEnumerable<(FactDefinition, T)> GetFactsFromScene<T>(DNESaveFile.SavProperty Scene, string FactDictName, FactType ftype)
        {
            var factsStruct = Scene["Facts"] ?? throw new Exception("Facts missing");
            var FactAssetsMap = factsStruct["FactAssets"] as DNESaveFile.MapSavProperty<DNESaveFile.SavProperty> ?? throw new Exception("FactAssets missing");
            foreach (var kvp in FactAssetsMap.Map)
            {
                if (kvp.Value[FactDictName] is not DNESaveFile.MapSavProperty<T> ListProperty) continue;
                foreach (var kvpi in ListProperty.Map)
                {
                    yield return (new FactDefinition() { FactAssetId = kvp.Key, FactId = kvpi.Key, Type = ftype }, kvpi.Value);
                }
            }
        }

        public List<FactTimeline> CatalogFacts()
        {
            List<FactTimeline> UniqueFacts = [];
            DNESaveFile.SavProperty SceneSnapshots = savefile["SceneSnapshots"] ?? throw new Exception("SceneSnapshots not found.");

            FactTimeline timelineForFact(FactDefinition fd)
            {
                var a = UniqueFacts.FirstOrDefault(a => a.Fact.FactAssetId == fd.FactAssetId && a.Fact.FactId == fd.FactId);
                if (a != null) return a;
                a = new FactTimeline() { Fact = FactDefinitionHelper.FindFactDefinition(fd.FactId, fd.FactAssetId) ?? fd, ValueTimeline = [] };
                UniqueFacts.Add(a);
                return a;
            }

            void ProcessScene(DNESaveFile.SavProperty scene, string key)
            {
                foreach (var (fact, fvalue) in GetFactsFromScene<bool>(scene, "BoolFacts", FactType.BoolFact))
                {
                    var tl = timelineForFact(fact);
                    tl.ValueTimeline[key] = new FactValue() { BoolValue = fvalue };
                }
                foreach (var (fact, fvalue) in GetFactsFromScene<int>(scene, "IntFacts", FactType.IntFact))
                {
                    var tl = timelineForFact(fact);
                    tl.ValueTimeline[key] = new FactValue() { IntValue = fvalue };
                }
                foreach (var (fact, fvalue) in GetFactsFromScene<byte>(scene, "EnumFacts", FactType.EnumFact))
                {
                    var tl = timelineForFact(fact);
                    tl.ValueTimeline[key] = new FactValue() { EnumValue = fvalue };
                }
                foreach (var (fact, fvalue) in GetFactsFromScene<float>(scene, "FloatFacts", FactType.FloatFact))
                {
                    var tl = timelineForFact(fact);
                    tl.ValueTimeline[key] = new FactValue() { FloatValue = fvalue };
                }
            }

            foreach (var SceneId in SceneNameMappings)
            {

                var scene = SceneSnapshots[SceneId.Key];
                if (scene == null)
                {
                    Console.WriteLine($"Warning: Scene {SceneId.Key} not found.");
                    continue;
                }

                ProcessScene(scene, SceneId.Key);
            }

            var currentSnapshot = savefile["CurrentSnapshot"] ?? throw new Exception("CurrentSnapshot not found.");
            ProcessScene(currentSnapshot, "global");

            UniqueFacts = UniqueFacts.OrderBy(a => a.FirstScene).ThenBy(a => a.Fact.FactAssetId).ThenBy(a => a.Fact.FactId).ToList();

            foreach (var fact in UniqueFacts)
            {
                Console.WriteLine(fact.GetHistoryString());
                Console.WriteLine();
            }

            return UniqueFacts;
        }

    }
}