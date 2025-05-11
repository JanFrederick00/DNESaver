
using System.IO;
using System.Text.Json;

namespace DNESaver
{
    class UnrealFactDef
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public string Class { get; set; }
        public string? Outer { get; set; }
        public UDefProperties Properties { get; set; } = new();

        public class UDefProperties
        {
            public class tEnumType
            {
                public string ObjectName { get; set; }
                public string ObjectPath { get; set; }
            }
            public tEnumType? EnumType { get; set; }

            public class tFactName
            {
                public string Name { get; set; }
                public string NameGuid { get; set; }
            }
            public tFactName? FactName { get; set; }
            public string Family { get; set; }
            public string FactAssetId { get; set; }

            public List<tDisplayNameMap>? DisplayNameMap { get; set; }
            public class tDisplayNameMap
            {
                public string Key { get; set; }

                public tValue Value { get; set; }

                public class tValue
                {
                    public string Namespace { get; set; }
                    public string Key { get; set; }
                    public string SourceString { get; set; }
                    public string CultureInvariantString { get; set; }
                }
            }
        }

        public Dictionary<string, int>? Names { get; set; }


        public static IEnumerable<FactDefinition> GenerateFactDefinitionsFromGameData(string filename)
        {
            string json = File.ReadAllText(filename);
            UnrealFactDef[][] uDefs = JsonSerializer.Deserialize<UnrealFactDef[][]>(json) ?? throw new Exception($"Could not load {filename}");
            var factDataDefs = uDefs.SelectMany(s => s).Where(a => a.Type.StartsWith("DNEFactData_", StringComparison.InvariantCultureIgnoreCase)).ToArray();

            foreach (var factData in factDataDefs)
            {
                if (factData == null) continue;
                var outer = uDefs.SelectMany(s => s).FirstOrDefault(a => a.Name == factData.Outer);
                if (outer == null)
                    continue;

                string FactName = factData?.Properties?.FactName?.Name ?? "";
                string FactId = factData?.Properties?.FactName?.NameGuid ?? "";
                string FactAssetId = outer.Properties?.FactAssetId ?? "";
                string FactAssetBame = outer.Name ?? "";
                string Family = factData?.Properties?.Family ?? "";

                FactType? ftype = factData?.Type switch
                {
                    "DNEFactData_Bool" => FactType.BoolFact,
                    "DNEFactData_Int" => FactType.IntFact,
                    "DNEFactData_Float" => FactType.FloatFact,
                    "DNEFactData_Enum" => FactType.EnumFact,
                    _ => null
                };
                if (ftype == null)
                    continue;

                var fdef = new FactDefinition()
                {
                    FactAssetName = FactAssetBame,
                    FactAssetId = FactAssetId,
                    Family = Family,
                    FactId = FactId,
                    FactName = FactName,
                    Type = ftype.Value,
                    EnumValues = []
                };

                if (ftype == FactType.EnumFact)
                {
                    string enumType = factData.Properties.EnumType?.ObjectName ?? "";
                    if (!string.IsNullOrWhiteSpace(enumType))
                    {
                        if (enumType.StartsWith("UserDefinedEnum"))
                        {
                            string enumName = enumType["UserDefinedEnum".Length..].Trim('\'');
                            var enumdef = uDefs.SelectMany(s => s).FirstOrDefault(a => a.Name == enumName);
                            if (enumdef != null)
                            {
                                foreach (var kv in enumdef.Names ?? [])
                                {
                                    string sv = kv.Key;
                                    int idx = sv.IndexOf("::");
                                    if (idx > 0) sv = sv[idx..].TrimStart(':');
                                    var displayname = enumdef.Properties?.DisplayNameMap?.FirstOrDefault(a => a.Key == sv);

                                    sv = displayname?.Value?.SourceString ?? displayname?.Value?.CultureInvariantString ?? $"Value {kv.Value}";
                                    fdef.EnumValues.Add(new FactDefinition.FactEnumValue() { Numerical = (byte)kv.Value, Name = sv });
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Enum {enumName} not found.");
                            }
                        }
                    }
                }

                yield return fdef;
            }
        }
    }
}