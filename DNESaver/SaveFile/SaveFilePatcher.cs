using System.IO;

public class SaveFilePatcher
{
    private readonly string Filename;
    private readonly byte[] buffer;

    public SaveFilePatcher(string filename)
    {
        Filename = filename;
        buffer = File.ReadAllBytes(Filename);
    }

    private static byte[] ValToBytes<T>(T t)
    {
        using var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        if (t is int i) bw.Write(i);
        else if (t is uint i2) bw.Write(i2);
        else if (t is long i3) bw.Write(i3);
        else if (t is ulong i4) bw.Write(i4);
        else if (t is byte i5) bw.Write(i5);
        else if (t is bool b) bw.Write((byte)(b ? 0x01 : 0x00));
        else throw new Exception("Unsupported.");
        return ms.ToArray();
    }

    public void Patch<T>(DNESaveFile.NumericSavProperty<T> property, T newValue)
    {
        var bNewValue = ValToBytes(newValue);
        var bOldValue = ValToBytes(property.Value);

        if (bNewValue.Length != property.LengthInFile) throw new Exception("length mismatch");
        if (bOldValue.Length != property.LengthInFile) throw new Exception("length mismatch");

        for (long l = 0; l < property.LengthInFile; ++l)
        {
            long offs = property.ValueOffsetInFile + l;
            if (buffer[offs] != bOldValue[l]) throw new Exception("Old value does not match.");
            buffer[offs] = bNewValue[l];
        }

        Console.WriteLine($"Patch {property} -> {newValue}");
    }

    public void Patch<T>(DNESaveFile.MapSavProperty<T> property, string key, T newValue)
    {
        var bNewValue = ValToBytes(newValue);
        var bOldValue = ValToBytes(property.Map[key]);

        if (bNewValue.Length != property.ValueLengthInFile) throw new Exception("length mismatch");
        if (bOldValue.Length != property.ValueLengthInFile) throw new Exception("length mismatch");

        long baseOffset = property.ValueOffsetsInSavFile[key];

        for (long l = 0; l < property.ValueLengthInFile; ++l)
        {
            long offs = baseOffset + l;
            if (buffer[offs] != bOldValue[l]) throw new Exception("Old value does not match.");
            buffer[offs] = bNewValue[l];
        }

        Console.WriteLine($"Patch {key} -> {newValue}");
    }

    public void WriteOut()
    {
        File.WriteAllBytes(Filename, buffer);
    }
}