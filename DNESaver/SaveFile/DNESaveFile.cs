using DNESaver.oodle;
using System.IO;
using System.Text;

public class DNESaveFile
{
    public bool IsCompressed { get; private set; }
    public string FileName { get; private set; }
    private readonly Stream SaveFileDataStream;
    private readonly BinaryReader Reader;

    public List<SavProperty> Fields { get; private set; } = [];

    private string ReadString()
    {
        uint length = Reader.ReadUInt32();
        var data = Reader.ReadBytes((int)length);
        return Encoding.UTF8.GetString(data[..^1]);
    }

    private string PeekString()
    {
        var pos = SaveFileDataStream.Position;
        uint length = Reader.ReadUInt32();
        var data = Reader.ReadBytes((int)length);
        SaveFileDataStream.Position = pos;
        return Encoding.UTF8.GetString(data[..^1]);
    }

    private StructSavProperty ReadStructValues(SavStructFormat format, string propName, string propType, ulong propLength, bool headerOnly = false, string? first_key_specified = null)
    {
        StructSavProperty str = new() { DataLength = propLength, PropName = propName, PropType = propType, StructFormat = format, Fields = [] };

        if (headerOnly) return str; // sometimes the dataLength is given as 0;

        SavProperty? content;
        do
        {
            content = ReadProperty(true, first_key_specified);
            first_key_specified = null;
            if (content != null && content is not NoneSavProperty) str.Fields.Add(content);
        } while (content != null && content is not NoneSavProperty);

        return str;
    }
    private readonly static List<string> WarnedStructTypes = [];

    private string ReadUEGuid()
    {
        uint i1 = Reader.ReadUInt32();
        uint i2 = Reader.ReadUInt32();
        uint i3 = Reader.ReadUInt32();
        uint i4 = Reader.ReadUInt32();

        return $"{i1:X8}-{i2:X8}-{i3:X8}-{i4:X8}";
    }

    private SavProperty? ReadProperty(bool ExpectNoneTerminator = false, string? NameExt = null)
    {
        string propName = NameExt ?? ReadString();


        if (propName == "None")
        {
            if (ExpectNoneTerminator) return null; // yes, we are skipping the read of the further bytes. 

            uint unknown = Reader.ReadUInt32();
            uint unknown2 = 0;
            if (Reader.BaseStream.Position < Reader.BaseStream.Length)
            {
                unknown2 = Reader.ReadUInt32();
            }

            return new NoneSavProperty() { PropName = propName, DataLength = 0, PropType = "", Unknown1 = unknown, Unknown2 = unknown2 };
        }

        string propType = ReadString();
        ulong dataLength = Reader.ReadUInt64();

        switch (propType)
        {
            case "NameProperty":
                {
                    ReadEmpty(1);
                    string value = ReadString();
                    return new NameSavProperty() { DataLength = dataLength, PropName = propName, PropType = propType, Value = value };
                }
            case "StrProperty":
                {
                    ReadEmpty(1);
                    string value = ReadString();
                    return new StrSavProperty() { DataLength = dataLength, PropName = propName, PropType = propType, Value = value };
                }
            case "EnumProperty":
                {
                    string EnumType = ReadString();
                    ReadEmpty(1);

                    byte[] data = Reader.ReadBytes((int)dataLength);
                    string enumValue = Encoding.UTF8.GetString(data[4..]).TrimEnd('\u0000');
                    return new EnumSavProperty() { DataLength = dataLength, PropName = propName, PropType = propType, EnumType = EnumType, EnumValue = enumValue };
                }
            case "ByteProperty":
                {
                    if (dataLength == 1)
                    {
                        string sNone = ReadString();
                        if (sNone != "None")
                        {
                            Console.WriteLine("ByteProperty len = 1 no 'None'?");
                        }
                        ReadEmpty(1);
                        var offset = SaveFileDataStream.Position;
                        byte bValue = Reader.ReadByte();
                        return new NumericSavProperty<byte>() { DataLength = dataLength, PropName = propName, PropType = propType, Value = bValue, ValueOffsetInFile = offset, LengthInFile = 1 };
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
            case "IntProperty":
                {
                    ReadEmpty(1);
                    int value = 0;
                    var offset = SaveFileDataStream.Position;
                    if (dataLength != 4)
                    {
                        _ = Reader.ReadBytes((int)dataLength);
                    }
                    else
                    {

                        value = Reader.ReadInt32();
                    }
                    return new NumericSavProperty<int>() { DataLength = dataLength, PropName = propName, PropType = propType, Value = value, ValueOffsetInFile = offset, LengthInFile = 4 };
                }
            case "UInt32Property":
                {
                    ReadEmpty(1);
                    uint value = 0;
                    var offset = SaveFileDataStream.Position;
                    if (dataLength != 4)
                    {
                        _ = Reader.ReadBytes((int)dataLength);
                    }
                    else
                    {
                        value = Reader.ReadUInt32();
                    }
                    return new NumericSavProperty<uint>() { DataLength = dataLength, PropName = propName, PropType = propType, Value = value, ValueOffsetInFile = offset, LengthInFile = 4 };
                }
            case "Int64Property":
                {
                    ReadEmpty(1);
                    long value = 0;
                    var offset = SaveFileDataStream.Position;
                    if (dataLength != 8)
                    {
                        _ = Reader.ReadBytes((int)dataLength);
                    }
                    else
                    {
                        value = Reader.ReadInt64();
                    }
                    return new NumericSavProperty<long>() { DataLength = dataLength, PropName = propName, PropType = propType, Value = value, ValueOffsetInFile = offset, LengthInFile = 8 };
                }
            case "BoolProperty":
                {
                    var offset = SaveFileDataStream.Position;
                    byte bValue = Reader.ReadByte();
                    ReadEmpty(1);
                    return new NumericSavProperty<bool>() { DataLength = dataLength, PropName = propName, PropType = propType, Value = bValue != 0, ValueOffsetInFile = offset, LengthInFile = 1 };
                }
            case "FloatProperty":
                {
                    ReadEmpty(1);
                    var offset = SaveFileDataStream.Position;
                    float fValue = Reader.ReadSingle();
                    return new NumericSavProperty<float>() { DataLength = dataLength, PropName = propName, PropType = propType, Value = fValue, ValueOffsetInFile = offset, LengthInFile = 4 };
                }
            case "StructProperty":
                {
                    SavStructFormat StructFormat = new()
                    {
                        StructDataType = ReadString(),
                        StructHeader = Reader.ReadBytes(0x11),
                    };

                    if (StructFormat.StructDataType == "DateTime")
                    {
                        ulong nvalue = 0;
                        var offset = SaveFileDataStream.Position;
                        if (dataLength != 8)
                        {
                            Console.WriteLine("Warning: DateTime != 8?");
                            _ = Reader.ReadBytes((int)dataLength);
                        }
                        else
                        {
                            nvalue = Reader.ReadUInt64();
                        }

                        return new DateTimeStructSavProperty() { PropType = propType, PropName = propName, DataLength = dataLength, Value = nvalue, ValueOffsetInFile = offset, LengthInFile = 8 };
                    }
                    else if (StructFormat.StructDataType == "Color")
                    {
                        if (dataLength != 4)
                        {
                            Console.WriteLine("Warning: Color != 4?");
                        }
                        byte[] colorValues = Reader.ReadBytes((int)dataLength);
                        return new ColorStructSavProperty() { PropType = propType, PropName = propName, DataLength = dataLength, ColorData = colorValues };
                    }
                    else if (StructFormat.StructDataType == "Guid")
                    {
                        if (dataLength != 16)
                        {
                            Console.WriteLine("Warning: Guid != 16?");
                        }
                        byte[] guidValues = Reader.ReadBytes((int)dataLength);
                        return new GuidStructSavProperty() { PropType = propType, PropName = propName, DataLength = dataLength, GuidData = guidValues };
                    }
                    else
                    {
                        if (!StructFormat.StructDataType.StartsWith("UNI"))
                        {
                            if (!WarnedStructTypes.Contains(StructFormat.StructDataType))
                            {
                                Console.WriteLine($"WARNING: No special handling for struct type {StructFormat.StructDataType} - possibly wrong handling.");
                                WarnedStructTypes.Add(StructFormat.StructDataType);
                            }
                            byte[] unknownDataValues = Reader.ReadBytes((int)dataLength);
                            return new UnknownSimpleStructSavProperty() { PropType = propType, PropName = propName, DataLength = dataLength, Data = unknownDataValues, Format = StructFormat };
                        }

                        return ReadStructValues(StructFormat, propName, propType, dataLength, dataLength == 0);
                    }
                }
            case "MapProperty":
                {
                    string KeyType = ReadString();
                    string ValueType = ReadString();

                    ReadEmpty(1);
                    ReadEmpty(4);

                    uint numElements = Reader.ReadUInt32();

                    (string, bool) ReadKey()
                    {
                        var pos = SaveFileDataStream.Position;
                        var possibly_length = Reader.ReadUInt32();
                        SaveFileDataStream.Position = pos;

                        if (possibly_length < 256)
                        {
                            return (ReadString(), KeyType == "StructProperty");
                        }
                        else
                        {
                            //return ($"'{string.Join("", Reader.ReadBytes(16).Select(s => s.ToString("X2")))}'", false);
                            //return (new Guid(Reader.ReadBytes(16)).ToString(), false);
                            return (ReadUEGuid(), false);
                        }
                    }

                    if (KeyType != "StructProperty" && KeyType != "NameProperty")
                    {
                        Console.WriteLine($"Warning: Map key is {KeyType}?");
                    }

                    var offset = SaveFileDataStream.Position;

                    switch (ValueType)
                    {
                        case "NameProperty":
                            {
                                var map = new MapSavProperty<string>() { PropType = propType, PropName = propName, DataLength = dataLength, KeyDataType = KeyType, ValueDataType = ValueType, Map = [] };

                                while (map.Map.Count < numElements)
                                {
                                    (string key, _) = ReadKey();
                                    var offs = SaveFileDataStream.Position;
                                    string value = ReadString();
                                    map.Map[key] = value;
                                    map.ValueOffsetsInSavFile[key] = offs;
                                    map.ValueLengthInFile = -1;
                                }
                                return map;
                            }
                        case "StructProperty":
                            {
                                var map = new MapSavProperty<SavProperty>() { PropType = propType, PropName = propName, DataLength = dataLength, KeyDataType = KeyType, ValueDataType = ValueType, Map = [] };

                                while (map.Map.Count < numElements)
                                {
                                    SavProperty? value;
                                    (string key, bool keyIsProperty) = ReadKey();

                                    if (keyIsProperty)
                                    {
                                        var actualKey = ReadStructValues(new SavStructFormat() { StructHeader = [], StructDataType = "CSK" }, "", "key", 0, false, key);
                                        key = actualKey.ToString();
                                    }

                                    var offs = SaveFileDataStream.Position;
                                    value = ReadStructValues(new SavStructFormat() { StructHeader = [], StructDataType = $"c{propName}" }, "", "", 0);

                                    if (map.Map.ContainsKey(key))
                                    {
                                        Console.WriteLine("Map duplicate key?");
                                    }
                                    map.Map[key] = value ?? throw new Exception("Null map value?");
                                    map.ValueLengthInFile = -1;
                                    map.ValueOffsetsInSavFile[key] = offs;
                                }
                                return map;
                            }
                        case "BoolProperty":
                            {
                                var map = new MapSavProperty<bool>() { PropType = propType, PropName = propName, DataLength = dataLength, KeyDataType = KeyType, ValueDataType = ValueType, Map = [] };

                                while (map.Map.Count < numElements)
                                {
                                    (string key, _) = ReadKey();

                                    var offs = SaveFileDataStream.Position;
                                    byte bValue = Reader.ReadByte();
                                    if (bValue != 0 && bValue != 1)
                                    {
                                        Console.WriteLine($"Warning: in Map<?,bool> -> unexpected bool value {bValue:X2}");
                                    }

                                    bool value = bValue != 0x00;

                                    map.Map[key] = value;
                                    map.ValueOffsetsInSavFile[key] = offs;
                                    map.ValueLengthInFile = 1;
                                }
                                return map;
                            }
                        case "ByteProperty":
                            {
                                var map = new MapSavProperty<byte>() { PropType = propType, PropName = propName, DataLength = dataLength, KeyDataType = KeyType, ValueDataType = ValueType, Map = [] };

                                while (map.Map.Count < numElements)
                                {
                                    (string key, _) = ReadKey();
                                    var offs = SaveFileDataStream.Position;
                                    byte value = Reader.ReadByte();
                                    map.Map[key] = value;
                                    map.ValueOffsetsInSavFile[key] = offs;
                                    map.ValueLengthInFile = 1;
                                }
                                return map;
                            }
                        case "IntProperty":
                            {
                                var map = new MapSavProperty<int>() { PropType = propType, PropName = propName, DataLength = dataLength, KeyDataType = KeyType, ValueDataType = ValueType, Map = [] };

                                while (map.Map.Count < numElements)
                                {
                                    (string key, _) = ReadKey();
                                    var offs = SaveFileDataStream.Position;
                                    int value = Reader.ReadInt32();
                                    map.Map[key] = value;
                                    map.ValueOffsetsInSavFile[key] = offs;
                                    map.ValueLengthInFile = 4;
                                }
                                return map;
                            }
                        case "FloatProperty":
                            {
                                var map = new MapSavProperty<float>() { PropType = propType, PropName = propName, DataLength = dataLength, KeyDataType = KeyType, ValueDataType = ValueType, Map = [] };

                                while (map.Map.Count < numElements)
                                {
                                    (string key, _) = ReadKey();
                                    var offs = SaveFileDataStream.Position;
                                    float value = Reader.ReadSingle();
                                    map.Map[key] = value;
                                    map.ValueOffsetsInSavFile[key] = offs;
                                    map.ValueLengthInFile = 4;
                                }
                                return map;
                            }
                        default:
                            if (numElements == 0)
                            {
                                Console.WriteLine($"Warning: Map of not-yet-supported type {ValueType}, but len = 0; ignoring for now.");
                                return new MapSavProperty<string>() { PropType = propType, PropName = propName, DataLength = dataLength, KeyDataType = KeyType, ValueDataType = ValueType, Map = [] };
                            }
                            throw new Exception($"Map: Unsupported value type: {ValueType}");

                    }
                }
            case "ArrayProperty":
                {
                    string DataType = ReadString();
                    ReadEmpty(1);

                    uint NumArrayElements = Reader.ReadUInt32();

                    switch (DataType)
                    {
                        case "StructProperty":
                            {
                                var arr = new ArraySavProperty<SavProperty>() { PropType = propType, DataLength = dataLength, PropName = propName, Array = [], DataType = DataType };
                                arr.Array.Capacity = (int)NumArrayElements;

                                SavProperty? content = null;
                                while (arr.Array.Count < NumArrayElements)
                                {
                                    if (content is StructSavProperty lastStruct)
                                    {
                                        content = ReadStructValues(lastStruct.StructFormat, lastStruct.PropName, lastStruct.PropType, lastStruct.DataLength);
                                    }
                                    else
                                    {
                                        content = ReadProperty(true);
                                    }

                                    if (content == null)
                                    {
                                        Console.WriteLine("Warning: Array premature end!");
                                        break;
                                    }

                                    arr.Array.Add(content);
                                }

                                return arr;
                            }
                        case "IntProperty":
                            {
                                var arr = new ArraySavProperty<int>() { PropType = propType, DataLength = dataLength, PropName = propName, Array = [], DataType = DataType };
                                arr.Array.Capacity = (int)NumArrayElements;

                                while (arr.Array.Count < NumArrayElements)
                                {
                                    arr.Array.Add(Reader.ReadInt32());
                                }

                                return arr;
                            }
                        case "NameProperty":
                            {
                                var arr = new ArraySavProperty<string>() { PropType = propType, DataLength = dataLength, PropName = propName, Array = [], DataType = DataType };
                                arr.Array.Capacity = (int)NumArrayElements;

                                while (arr.Array.Count < NumArrayElements)
                                {
                                    arr.Array.Add(ReadString());
                                }

                                return arr;
                            }
                        default:
                            if (NumArrayElements == 0)
                            {
                                Console.WriteLine($"Warning: Array of not-yet-supported type {DataType}, but len = 0; ignoring for now.");
                                return new ArraySavProperty<SavProperty>() { PropType = propType, DataLength = dataLength, PropName = propName, Array = [], DataType = DataType };
                            }
                            throw new Exception();
                    }
                }
            default:
                Console.WriteLine($"UNKNOWN TYPE {propType} OF FIELD {propName}");
                return null;
        }
    }

    private void ReadEmpty(int length)
    {
        long baseOffset = Reader.BaseStream.Position;
        var bytes = Reader.ReadBytes(length);
        for (int i = 0; i < bytes.Length; ++i)
        {
            if (bytes[i] != 0x00)
            {
                Console.WriteLine($"WARNING: byte @ {(baseOffset + i):X6} != 0x00: {bytes[i]:X2}");
            }
        }
    }

    public long UncompressedFileLength
    {
        get
        {
            return SaveFileDataStream.Length;
        }
    }

    public string SaveDataType { get; private set; }
    public string EngineVersion { get; private set; }
    public uint UnknownHeaderValue { get; private set; }

    public DNESaveFile(string filename)
    {
        FileName = filename;

        using var filestream = File.OpenRead(FileName);
        using var reader = new BinaryReader(filestream);

        var filemagic = reader.ReadBytes(8);
        string filemagic_s = Encoding.ASCII.GetString(filemagic);
        if (filemagic_s != "@DNESAV@") throw new Exception("Expected @DNESAV@");

        ulong unknown1 = reader.ReadUInt64();
        if (unknown1 == 0x04)
        {
            SaveFileDataStream = new MemoryStream();
            filestream.Seek(0, SeekOrigin.Begin);
            filestream.CopyTo(SaveFileDataStream);

            Console.WriteLine("Save file is not compressed.");
        }
        else
        {
            if (unknown1 != 0x590c6eb518e9b0c3)
            {
                Console.WriteLine($"Warning: unk1 is {unknown1:X16}");
            }
            IsCompressed = true;

            // compressed save file - read lengths & decompress (oodle)
            uint uncompressedLength = reader.ReadUInt32();
            uint compressedLength = reader.ReadUInt32();
            byte[] compressedData = reader.ReadBytes((int)compressedLength);
            byte[] decompressedData = Oodle.Decompress(compressedData, (int)uncompressedLength);
            decompressedData = [.. "@DNESAV@"u8, .. decompressedData];

            Console.WriteLine($"Decompressed save file from {compressedData.Length} bytes -> {decompressedData.Length} bytes");

            SaveFileDataStream = new MemoryStream(decompressedData);
        }

        // seek to start of data

        SaveFileDataStream.Position = 16;
        this.Reader = new BinaryReader(SaveFileDataStream);

        // begin reading file
        SaveDataType = ReadString();
        UnknownHeaderValue = Reader.ReadUInt32();
        EngineVersion = ReadString();
        Console.WriteLine($"Engine Version: {EngineVersion}, Data Type: {SaveDataType}");

        SavProperty? prop = null;
        do
        {
            prop = ReadProperty();
            if (prop != null && prop is not NoneSavProperty) Fields.Add(prop);

        } while (SaveFileDataStream.Position < SaveFileDataStream.Length);
    }

    public SavProperty? this[string key]
    {
        get => Fields.FirstOrDefault(a => a.PropName.Equals(key, StringComparison.InvariantCultureIgnoreCase));
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{SaveDataType}");
        sb.AppendLine($"Engine ver.: {EngineVersion}");
        sb.AppendLine();
        foreach (var field in Fields)
        {
            sb.AppendLine(field.ToString(3));
        }
        return sb.ToString();
    }

    public void WriteSavegameTo(string filename)
    {
        var pos = SaveFileDataStream.Position;

        SaveFileDataStream.Position = 0;
        using (var file = File.Create(filename))
        {
            SaveFileDataStream.CopyTo(file);
        }

        SaveFileDataStream.Position = pos;
    }

    public struct SavStructFormat
    {
        public string StructDataType { get; set; }
        public byte[] StructHeader { get; set; }
    }

    public class StructSavProperty : SavProperty
    {
        public SavStructFormat StructFormat { get; set; }
        public List<SavProperty> Fields { get; set; } = [];

        public override string ToString(int indent)
        {
            var sb = new StringBuilder();
            if (!String.IsNullOrWhiteSpace(PropName))
            {
                sb.AppendLine($"{Indent(indent)} {PropType} {PropName} = struct {StructFormat.StructDataType} {{");
            }
            else
            {
                sb.AppendLine($"{Indent(indent)} struct {StructFormat.StructDataType} {{");
            }

            foreach (var field in Fields)
            {
                sb.AppendLine(field.ToString(indent + 4));
            }

            sb.Append($"{Indent(indent)} }}");

            return sb.ToString();
        }
        public override string ToString() => ToString(0);

        public override IEnumerable<SavProperty> Children()
        {
            foreach (var f in Fields)
            {
                yield return f;
            }
        }

        public override SavProperty? this[string key]
        {
            get => Fields.FirstOrDefault(a => a.PropName.Equals(key, StringComparison.InvariantCultureIgnoreCase));
        }
    }

    public class ArraySavProperty<T> : SavProperty
    {
        public required string DataType { get; set; }
        public List<T> Array { get; set; } = [];

        public override string ToString(int indent)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Indent(indent)} {PropType} {PropName} = {DataType} [");

            foreach (var field in Array)
            {
                if (field is SavProperty sprop) sb.AppendLine(sprop.ToString(indent + 4));
                else sb.AppendLine($"{Indent(indent + 4)} {field}");
            }

            sb.Append($"{Indent(indent)} ]");

            return sb.ToString();
        }
        public override string ToString() => ToString(0);

        public override IEnumerable<SavProperty> Children()
        {
            foreach (var f in Array)
            {
                if (f is SavProperty sf)
                    yield return sf;
            }
        }

        public override SavProperty? this[string key]
        {
            get => Array.OfType<SavProperty>().FirstOrDefault(a => a.PropName.Equals(key, StringComparison.InvariantCultureIgnoreCase));
        }
    }

    public class MapSavProperty<T> : SavProperty
    {
        public required string KeyDataType { get; set; }
        public required string ValueDataType { get; set; }
        public Dictionary<string, T> Map { get; set; } = [];

        public override string ToString(int indent)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Indent(indent)} {PropType} {PropName} = ({KeyDataType},{ValueDataType}) [");

            foreach (var kvp in Map)
            {
                if (kvp.Value is SavProperty sprop) sb.AppendLine($"{Indent(indent + 4)} {kvp.Key} = {sprop.ToString(indent + 4).TrimStart()}");
                else sb.AppendLine($"{Indent(indent + 4)} {kvp.Key} = {kvp.Value}");
            }

            sb.Append($"{Indent(indent)} ]");

            return sb.ToString();
        }
        public override string ToString() => ToString(0);

        public override IEnumerable<SavProperty> Children()
        {
            foreach (var f in Map.Values)
            {
                if (f is SavProperty sf)
                    yield return sf;
            }
        }

        public override SavProperty? this[string key]
        {
            get => Map.FirstOrDefault(a => a.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase)).Value as SavProperty;
        }

        public Dictionary<string, long> ValueOffsetsInSavFile { get; set; } = [];
        public long ValueLengthInFile { get; set; } = 0;
    }

    public class DateTimeStructSavProperty : NumericSavProperty<ulong>
    {
        public DateTime DateValue
        {
            get => new DateTime(1, 1, 1).AddMilliseconds(Value / 10000);
            set => Value = (ulong)((value - new DateTime(1, 1, 1)).TotalMilliseconds) * 10000;
        }

        public override string ToString(int indent)
        {
            return $"{Indent(indent)} DateTime {PropName} = {DateValue:yyyy-MM-dd HH:mm:ss}";
        }
        public override string ToString() => ToString(0);
    }

    public class ColorStructSavProperty : SavProperty
    {
        public required byte[] ColorData { get; set; }

        public override string ToString(int indent)
        {
            return $"{Indent(indent)} Color {PropName} = #{string.Concat(ColorData.Select(s => s.ToString("X2")))}";
        }
        public override string ToString() => ToString(0);
    }

    public class GuidStructSavProperty : SavProperty
    {
        public required byte[] GuidData { get; set; }
        public Guid Guid
        {
            get => new(GuidData);
            set => GuidData = value.ToByteArray();
        }

        public override string ToString(int indent)
        {
            return $"{Indent(indent)} Guid {PropName} = {Guid}";
        }
        public override string ToString() => ToString(0);
    }

    public class UnknownSimpleStructSavProperty : SavProperty
    {
        public required byte[] Data { get; set; }
        public SavStructFormat Format { get; set; }

        public override string ToString(int indent)
        {
            return $"{Indent(indent)} {PropType} {PropName} = [unknown {Format.StructDataType}] {String.Join(" ", Data.Select(s => s.ToString("X2")))}";
        }
        public override string ToString() => ToString(0);
    }

    public class SavProperty
    {
        public required string PropName { get; set; }
        public required string PropType { get; set; }
        public required ulong DataLength { get; set; }

        internal static string Indent(int i) => new(' ', i);
        public virtual string ToString(int indent)
        {
            return $"{Indent(indent)} {PropType} {PropName} = ? (len={DataLength:X4})";
        }
        public override string ToString() => ToString(0);


        public virtual SavProperty? this[string key]
        {
            get => null;
        }

        public virtual IEnumerable<SavProperty> Children() => [];
    }

    public class NoneSavProperty : SavProperty
    {
        public uint Unknown1 { get; set; }
        public uint Unknown2 { get; set; }

        public override string ToString(int indent)
        {
            return $"{Indent(indent)} NONE";
        }
        public override string ToString() => ToString(0);
    }


    public class StrSavProperty : SavProperty
    {
        public required string Value { get; set; }

        public override string ToString(int indent)
        {
            return $"{Indent(indent)} {PropType} {PropName} = {Value}";
        }
        public override string ToString() => ToString(0);
    }

    public class NameSavProperty : StrSavProperty { }

    public class EnumSavProperty : SavProperty
    {
        public required string EnumType { get; set; }
        public required string EnumValue { get; set; }

        public override string ToString(int indent)
        {
            return $"{Indent(indent)} {PropType} {PropName} = {EnumValue} [type: {EnumType}]";
        }
        public override string ToString() => ToString(0);
    }

    public class NumericSavProperty<T> : SavProperty, IFormattable
    {
        public required T Value { get; set; }

        public override string ToString(int indent)
        {
            return $"{Indent(indent)} {PropType} {PropName} = {Value}";
        }
        public override string ToString() => ToString(0);
        public string ToString(string format)
        {
            return format switch
            {
                "v" or "" => Value?.ToString() ?? "",
                "a" => $"{Value} {{{ValueOffsetInFile:X8}:{LengthInFile}}}",
                _ => ""
            };
        }

        public string ToString(string? format, IFormatProvider? formatProvider) => ToString(format ?? "");

        public required long ValueOffsetInFile { get; set; }
        public required long LengthInFile { get; set; }


    }
}

