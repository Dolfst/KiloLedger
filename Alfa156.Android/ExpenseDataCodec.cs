using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Alfa156.Android;

internal static class ExpenseDataCodec
{
    internal static readonly string[] DataFiles =
    {
        "cars.dat", "fuel.dat", "repairs.dat", "utilities.dat"
    };

    internal static bool IsDataFile(string name)
    {
        for (int i = 0; i < DataFiles.Length; i++)
            if (String.Equals(name, DataFiles[i], StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    internal static List<string[]> ReadRows(string path)
    {
        List<string[]> rows = new List<string[]>();
        using (StreamReader reader = new StreamReader(path, Encoding.UTF8, true))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
                if (line.Length > 0)
                    rows.Add(DecodeRow(line));
        }
        return rows;
    }

    internal static string[] DecodeRow(string line)
    {
        List<string> fields = new List<string>();
        StringBuilder field = new StringBuilder();
        bool escaped = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (escaped)
            {
                field.Append(c);
                escaped = false;
            }
            else if (c == '\\')
                escaped = true;
            else if (c == '|')
            {
                fields.Add(field.ToString());
                field.Length = 0;
            }
            else
                field.Append(c);
        }
        if (escaped)
            field.Append('\\');
        fields.Add(field.ToString());
        return fields.ToArray();
    }

    internal static void ValidateDirectory(string directory)
    {
        for (int i = 0; i < DataFiles.Length; i++)
            if (!File.Exists(Path.Combine(directory, DataFiles[i])))
                throw new InvalidDataException("Нужны все четыре файла: cars.dat, fuel.dat, repairs.dat и utilities.dat.");

        int[] columns = { 1, 5, 9, 8 };
        for (int i = 0; i < DataFiles.Length; i++)
        {
            List<string[]> rows = ReadRows(Path.Combine(directory, DataFiles[i]));
            for (int j = 0; j < rows.Count; j++)
                if (rows[j].Length != columns[i])
                    throw new InvalidDataException("Неверный формат записи в " + DataFiles[i] + ".");
            if (i == 0 && rows.Count == 0)
                throw new InvalidDataException("В cars.dat нет автомобилей.");
        }
    }

    internal static decimal Sum(string[] row, int startIndex)
    {
        decimal total = 0;
        for (int i = startIndex; i < row.Length; i++)
        {
            decimal amount;
            string value = row[i].Trim().Replace(',', '.');
            if (Decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
                total += amount;
        }
        return total;
    }
}
