using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

namespace DNESaver
{
    public class FactDefinition
    {
        public FactType Type { get; set; }
        public string FactAssetId { get; set; }
        public string FactId { get; set; }

        public string FactAssetName { get; set; }
        public string FactName { get; set; }
        public string Family { get; set; }
        public List<FactEnumValue> EnumValues { get; set; }

        public class FactEnumValue
        {
            public string Name { get; set; }
            public byte Numerical { get; set; }
        }
    }

    public enum FactType
    {
        IntFact = 0,
        FloatFact = 1,
        EnumFact = 2,
        BoolFact = 3
    }

    public struct FactValue
    {
        public int IntValue;
        public float FloatValue;
        public bool BoolValue;
        public byte EnumValue;

        public FactValue()
        {

        }

        public FactValue(bool b)
        {
            BoolValue = b;
        }

        public FactValue(int i)
        {
            IntValue = i;
        }

        public FactValue(float f)
        {
            FloatValue = f;
        }

        public FactValue(byte en)
        {
            EnumValue = en;
        }
    }

    public class FactDefinitionHelper
    {
        private static List<FactDefinition>? _facts = null;

        public static List<FactDefinition> GetDefinitions()
        {
            if (_facts != null) return _facts;

            string factsJsonName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fact_defs.json");
            if (!File.Exists(factsJsonName))
            {
                string gameassetsName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "facts.json");
                if (!File.Exists(gameassetsName))
                {
                    throw new Exception("The Fact definiton file (fact_defs.json) is not present and no facts.json was found to generate it.");
                }

                _facts = UnrealFactDef.GenerateFactDefinitionsFromGameData(gameassetsName).DistinctBy(a => (a.FactAssetId, a.FactId)).ToList();
                File.WriteAllText(factsJsonName, JsonSerializer.Serialize(_facts));
                Console.WriteLine($"The fact definition file (fact_defs.json) was generated.");
            }
            else
            {
                _facts = (JsonSerializer.Deserialize<List<FactDefinition>>(File.ReadAllText(factsJsonName)) ?? []);
            }

            return _facts;
        }

        public static FactDefinition? FindFactDefinition(string factId, string? assetId)
        {
            var facts = GetDefinitions();
            var candidates = facts.Where(a => a.FactId == factId).ToList();
            if (assetId != null) candidates = candidates.Where(a => a.FactAssetId == assetId).ToList();
            if (candidates.Count == 0)
            {
                Console.WriteLine($"Warning: fact definition not found: {assetId} / {factId}");
            }
            if (candidates.Count > 1)
            {
                Console.WriteLine($"Warning: multiple candidates for fact {assetId} / {factId}");
            }

            return candidates.FirstOrDefault();
        }

        public static FactDefinition? FindFactDefinitionByName(string name)
        {
            var facts = GetDefinitions();
            var candidates = facts.Where(a => a.FactName == name).ToList();

            if (candidates.Count == 0)
            {
                Console.WriteLine($"Warning: fact definition not found: {name}");
            }

            if (candidates.Count > 1)
            {
                Console.WriteLine($"Warning: multiple candidates for fact {name}");
            }

            return candidates.FirstOrDefault();
        }
    }
}