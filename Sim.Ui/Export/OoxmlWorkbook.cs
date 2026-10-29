using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Sim.Ui.Export;

/// <summary>One worksheet: a name, column widths (characters) and rows of text cells ("" = no cell).</summary>
public sealed class SheetModel(string name, int[] columnWidths)
{
    public string Name { get; } = name;
    public int[] ColumnWidths { get; } = columnWidths;
    public List<string[]> Rows { get; } = [];
}

/// <summary>
/// ADR-059 / D-066 A14 — A HAND-ROLLED, DETERMINISTIC OOXML (.xlsx) WRITER. No package: the workbook is a ZIP of a
/// handful of fixed XML parts, written here byte by byte.
///
/// DETERMINISM. Every entry is STORED (no deflate — a runtime's zlib may differ, stored bytes cannot), in a FIXED
/// order, with a FIXED timestamp (1980-01-01 00:00, the DOS epoch), zero attributes, "version made by" 2.0 / MS-DOS,
/// no extra fields and no data descriptors; the archive is assembled in a seekable MemoryStream. Every cell is an
/// INLINE STRING (no shared-string table whose order could drift; no number a spreadsheet could re-format), escaped for
/// XML and for OOXML's own <c>_xHHHH_</c> convention, with <c>xml:space="preserve"</c>. The same sheets therefore give
/// the same bytes on every runtime and OS (A14).
///
/// WHY THE CONTAINER IS WRITTEN BY HAND (measured, ADR-059 §3): System.IO.Compression's ZipArchive writes the HOST OS
/// into each central-directory "version made by" field (0x0314 = Unix on Linux; 0x00xx on Windows), so its bytes are
/// OS-dependent even with STORED entries, fixed times and zero attributes. The reader below uses ZipArchive — an
/// independent implementation — to read the archive back.
/// </summary>
public static class OoxmlWorkbook
{
    /// <summary>Excel's limits (ECMA-376 / Excel specifications): characters per cell and rows per sheet.</summary>
    public const int MaxCellChars = 32767;
    public const int MaxRows = 1048576;

    private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PkgRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string Decl = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n";

    /// <summary>The workbook's bytes. Sheet names must be unique, 1–31 characters, without <c>[]:*?/\</c>.</summary>
    public static byte[] Write(IReadOnlyList<SheetModel> sheets)
    {
        if (sheets.Count == 0) throw new ArgumentException("a workbook needs at least one sheet");
        var parts = new List<(string Name, byte[] Data)>
        {
            ("[Content_Types].xml", Utf8(ContentTypes(sheets.Count))),
            ("_rels/.rels", Utf8(Decl + "<Relationships xmlns=\"" + PkgRelNs + "\"><Relationship Id=\"rId1\" Type=\""
                + RelNs + "/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>")),
            ("xl/workbook.xml", Utf8(WorkbookXml(sheets))),
            ("xl/_rels/workbook.xml.rels", Utf8(WorkbookRels(sheets.Count))),
            ("xl/styles.xml", Utf8(Styles)),
        };
        for (int i = 0; i < sheets.Count; i++)
            parts.Add(("xl/worksheets/sheet" + (i + 1).ToString(CultureInfo.InvariantCulture) + ".xml", Utf8(SheetXml(sheets[i]))));
        return StoredZip.Write(parts);
    }

    private static byte[] Utf8(string s) => new UTF8Encoding(false).GetBytes(s);

    private static string ContentTypes(int n)
    {
        var sb = new StringBuilder(Decl);
        sb.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">")
          .Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>")
          .Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>")
          .Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>")
          .Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
        for (int i = 1; i <= n; i++)
            sb.Append("<Override PartName=\"/xl/worksheets/sheet").Append(i.ToString(CultureInfo.InvariantCulture))
              .Append(".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        return sb.Append("</Types>").ToString();
    }

    private static string WorkbookXml(IReadOnlyList<SheetModel> sheets)
    {
        var sb = new StringBuilder(Decl);
        sb.Append("<workbook xmlns=\"").Append(MainNs).Append("\" xmlns:r=\"").Append(RelNs).Append("\"><sheets>");
        for (int i = 0; i < sheets.Count; i++)
        {
            string name = sheets[i].Name;
            if (name.Length is 0 or > 31 || name.IndexOfAny(['[', ']', ':', '*', '?', '/', '\\']) >= 0)
                throw new ArgumentException("invalid sheet name: " + name);
            string n = (i + 1).ToString(CultureInfo.InvariantCulture);
            sb.Append("<sheet name=\"");
            EscapeXml(sb, name, attribute: true);
            sb.Append("\" sheetId=\"").Append(n).Append("\" r:id=\"rId").Append(n).Append("\"/>");
        }
        return sb.Append("</sheets></workbook>").ToString();
    }

    private static string WorkbookRels(int n)
    {
        var sb = new StringBuilder(Decl);
        sb.Append("<Relationships xmlns=\"").Append(PkgRelNs).Append("\">");
        for (int i = 1; i <= n; i++)
        {
            string s = i.ToString(CultureInfo.InvariantCulture);
            sb.Append("<Relationship Id=\"rId").Append(s).Append("\" Type=\"").Append(RelNs)
              .Append("/worksheet\" Target=\"worksheets/sheet").Append(s).Append(".xml\"/>");
        }
        sb.Append("<Relationship Id=\"rId").Append((n + 1).ToString(CultureInfo.InvariantCulture)).Append("\" Type=\"")
          .Append(RelNs).Append("/styles\" Target=\"styles.xml\"/>");
        return sb.Append("</Relationships>").ToString();
    }

    private const string Styles = Decl
        + "<styleSheet xmlns=\"" + MainNs + "\">"
        + "<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>"
        + "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>"
        + "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>"
        + "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>"
        + "<cellXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/></cellXfs>"
        + "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>"
        + "</styleSheet>";

    private static string SheetXml(SheetModel sheet)
    {
        if (sheet.Rows.Count > MaxRows)
            throw new SessionExportException(SessionExportException.RowLimit,
                $"sheet {sheet.Name} has {sheet.Rows.Count} rows, over the {MaxRows} limit");
        var sb = new StringBuilder(Decl);
        sb.Append("<worksheet xmlns=\"").Append(MainNs).Append("\">");
        if (sheet.ColumnWidths.Length > 0)
        {
            sb.Append("<cols>");
            for (int c = 0; c < sheet.ColumnWidths.Length; c++)
            {
                string n = (c + 1).ToString(CultureInfo.InvariantCulture);
                sb.Append("<col min=\"").Append(n).Append("\" max=\"").Append(n).Append("\" width=\"")
                  .Append(sheet.ColumnWidths[c].ToString(CultureInfo.InvariantCulture)).Append("\" customWidth=\"1\"/>");
            }
            sb.Append("</cols>");
        }
        sb.Append("<sheetData>");
        for (int r = 0; r < sheet.Rows.Count; r++)
        {
            string rn = (r + 1).ToString(CultureInfo.InvariantCulture);
            sb.Append("<row r=\"").Append(rn).Append("\">");
            string[] row = sheet.Rows[r];
            for (int c = 0; c < row.Length; c++)
            {
                string text = row[c] ?? "";
                if (text.Length == 0) continue;
                if (text.Length > MaxCellChars)
                    throw new SessionExportException(SessionExportException.CellTooLarge,
                        $"sheet {sheet.Name} cell {ColumnName(c)}{rn} has {text.Length} characters, over the {MaxCellChars} limit");
                sb.Append("<c r=\"").Append(ColumnName(c)).Append(rn).Append("\" t=\"inlineStr\"><is><t xml:space=\"preserve\">");
                EscapeXml(sb, text, attribute: false);
                sb.Append("</t></is></c>");
            }
            sb.Append("</row>");
        }
        return sb.Append("</sheetData></worksheet>").ToString();
    }

    /// <summary>A, B, …, Z, AA, … for a 0-based column.</summary>
    public static string ColumnName(int c)
    {
        string s = "";
        int n = c + 1;
        while (n > 0)
        {
            int m = (n - 1) % 26;
            s = (char)('A' + m) + s;
            n = (n - 1) / 26;
        }
        return s;
    }

    /// <summary>
    /// XML-escapes cell text, then applies OOXML's ST_Xstring convention: a character XML cannot carry (a control
    /// character other than tab and line feed — carriage return included, which XML would normalize — U+FFFE, U+FFFF or
    /// an unpaired surrogate) is written <c>_xHHHH_</c>, and an underscore that would itself start such a sequence is
    /// written <c>_x005F_</c>, so every text reads back exactly.
    /// </summary>
    public static void EscapeXml(StringBuilder sb, string text, bool attribute)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (char.IsHighSurrogate(ch) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                sb.Append(ch).Append(text[i + 1]);
                i++;
                continue;
            }
            switch (ch)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"' when attribute: sb.Append("&quot;"); break;
                case '_' when IsXEscape(text, i): sb.Append("_x005F_"); break;
                default:
                    if ((ch < 0x20 && ch != '\t' && ch != '\n') || ch == '￾' || ch == '￿' || char.IsSurrogate(ch)
                        || (attribute && (ch == '\t' || ch == '\n')))
                        sb.Append("_x").Append(((int)ch).ToString("X4", CultureInfo.InvariantCulture)).Append('_');
                    else sb.Append(ch);
                    break;
            }
        }
    }

    /// <summary>Whether <c>_xHHHH_</c> starts at <paramref name="i"/>.</summary>
    internal static bool IsXEscape(string s, int i)
    {
        if (i + 7 > s.Length || s[i] != '_' || s[i + 1] != 'x' || s[i + 6] != '_') return false;
        for (int k = i + 2; k < i + 6; k++)
            if (!char.IsAsciiHexDigit(s[k])) return false;
        return true;
    }

    /// <summary>The inverse of the <c>_xHHHH_</c> step of <see cref="EscapeXml"/> (XML entities are the parser's).</summary>
    public static string UnescapeX(string s)
    {
        if (s.IndexOf("_x", StringComparison.Ordinal) < 0) return s;
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (IsXEscape(s, i))
            {
                sb.Append((char)int.Parse(s.AsSpan(i + 2, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
                i += 6;
            }
            else sb.Append(s[i]);
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------------------------- reading --

    /// <summary>What <see cref="Read"/> found: sheet names in order and each sheet's rows (cells by column; "" = none).</summary>
    public sealed record Content(IReadOnlyList<string> SheetNames, IReadOnlyList<IReadOnlyList<string[]>> Sheets, IReadOnlyList<string> EntryNames);

    /// <summary>
    /// Reads a workbook back with System.IO.Compression's ZipArchive (an independent ZIP implementation) and
    /// System.Xml's XmlReader (well-formedness): checks the required parts and relationships are present and every part
    /// parses, and returns every inline-string cell's text. Any structural defect throws.
    /// </summary>
    public static Content Read(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes, writable: false), ZipArchiveMode.Read);
        var names = new List<string>();
        foreach (ZipArchiveEntry e in zip.Entries) names.Add(e.FullName);
        foreach (string required in new[] { "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "xl/styles.xml" })
            if (!names.Contains(required)) throw new InvalidDataException("missing part " + required);
        // every XML part must be well-formed
        foreach (ZipArchiveEntry e in zip.Entries)
        {
            using Stream s = e.Open();
            using XmlReader x = XmlReader.Create(s, ReaderSettings());
            while (x.Read()) { }
        }
        string types = EntryText(zip, "[Content_Types].xml");
        string rootRels = EntryText(zip, "_rels/.rels");
        if (!rootRels.Contains("Target=\"xl/workbook.xml\"", StringComparison.Ordinal))
            throw new InvalidDataException("the package relationship does not name xl/workbook.xml");

        // workbook.xml: sheet names and relationship ids, in order
        var sheetNames = new List<string>();
        var sheetRids = new List<string>();
        using (XmlReader x = XmlReader.Create(new StringReader(EntryText(zip, "xl/workbook.xml")), ReaderSettings()))
            while (x.Read())
                if (x.NodeType == XmlNodeType.Element && x.LocalName == "sheet" && x.NamespaceURI == MainNs)
                {
                    sheetNames.Add(x.GetAttribute("name") ?? throw new InvalidDataException("sheet without a name"));
                    sheetRids.Add(x.GetAttribute("id", RelNs) ?? throw new InvalidDataException("sheet without r:id"));
                }
        var relIds = new List<string>();
        var relTargets = new List<string>();
        using (XmlReader x = XmlReader.Create(new StringReader(EntryText(zip, "xl/_rels/workbook.xml.rels")), ReaderSettings()))
            while (x.Read())
                if (x.NodeType == XmlNodeType.Element && x.LocalName == "Relationship")
                {
                    relIds.Add(x.GetAttribute("Id")!);
                    relTargets.Add(x.GetAttribute("Target")!);
                }
        var sheets = new List<IReadOnlyList<string[]>>();
        foreach (string rid in sheetRids)
        {
            int at = relIds.IndexOf(rid);
            if (at < 0) throw new InvalidDataException("no relationship " + rid);
            string part = "xl/" + relTargets[at];
            if (!names.Contains(part)) throw new InvalidDataException("missing sheet part " + part);
            if (!types.Contains("PartName=\"/" + part + "\"", StringComparison.Ordinal))
                throw new InvalidDataException("no content type for " + part);
            sheets.Add(ReadSheet(zip, part));
        }
        return new Content(sheetNames, sheets, names);
    }

    private static XmlReaderSettings ReaderSettings() =>
        new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreWhitespace = false };

    private static string EntryText(ZipArchive zip, string name)
    {
        ZipArchiveEntry e = zip.GetEntry(name) ?? throw new InvalidDataException("missing part " + name);
        using var r = new StreamReader(e.Open(), new UTF8Encoding(false, true));
        return r.ReadToEnd();
    }

    private static List<string[]> ReadSheet(ZipArchive zip, string part)
    {
        var rows = new List<string[]>();
        ZipArchiveEntry entry = zip.GetEntry(part)!;
        using Stream s = entry.Open();
        using XmlReader x = XmlReader.Create(s, ReaderSettings());
        List<string>? row = null;
        int col = -1;
        while (x.Read())
        {
            if (x.NodeType == XmlNodeType.Element && x.LocalName == "row")
            {
                int r = int.Parse(x.GetAttribute("r")!, NumberStyles.None, CultureInfo.InvariantCulture);
                if (r != rows.Count + 1) throw new InvalidDataException($"{part}: row {r} out of order");
                row = [];
                if (x.IsEmptyElement) rows.Add([]);
            }
            else if (x.NodeType == XmlNodeType.EndElement && x.LocalName == "row")
            {
                rows.Add(row!.ToArray());
                row = null;
            }
            else if (x.NodeType == XmlNodeType.Element && x.LocalName == "c")
            {
                if (x.GetAttribute("t") != "inlineStr") throw new InvalidDataException($"{part}: a cell is not an inline string");
                col = ColumnIndex(x.GetAttribute("r")!);
            }
            else if (x.NodeType == XmlNodeType.Element && x.LocalName == "t")
            {
                if (x.GetAttribute("space", "http://www.w3.org/XML/1998/namespace") != "preserve")
                    throw new InvalidDataException($"{part}: a text without xml:space=\"preserve\"");
                string text = x.IsEmptyElement ? "" : x.ReadElementContentAsString();
                while (row!.Count < col) row.Add("");
                row.Add(UnescapeX(text));
            }
        }
        return rows;
    }

    private static int ColumnIndex(string cellRef)
    {
        int c = 0;
        foreach (char ch in cellRef)
        {
            if (ch < 'A' || ch > 'Z') break;
            c = c * 26 + (ch - 'A' + 1);
        }
        return c - 1;
    }
}

/// <summary>A minimal ZIP writer: STORED entries only, fixed time and attributes, no extras (ADR-059 §3).</summary>
public static class StoredZip
{
    /// <summary>DOS date of 1980-01-01 (the earliest a ZIP can carry) and time 00:00:00.</summary>
    public const ushort DosDate = (0 << 9) | (1 << 5) | 1, DosTime = 0;

    public static byte[] Write(IReadOnlyList<(string Name, byte[] Data)> entries)
    {
        using var ms = new MemoryStream();
        var offsets = new long[entries.Count];
        var crcs = new uint[entries.Count];
        Span<byte> h = stackalloc byte[46];
        for (int i = 0; i < entries.Count; i++)
        {
            (string name, byte[] data) = entries[i];
            byte[] n = Encoding.ASCII.GetBytes(name);
            offsets[i] = ms.Position;
            crcs[i] = Crc32(data);
            h.Clear();
            BinaryPrimitives.WriteUInt32LittleEndian(h, 0x04034b50);
            BinaryPrimitives.WriteUInt16LittleEndian(h[4..], 20);         // version needed: 2.0
            BinaryPrimitives.WriteUInt16LittleEndian(h[6..], 0);          // flags
            BinaryPrimitives.WriteUInt16LittleEndian(h[8..], 0);          // method: STORED
            BinaryPrimitives.WriteUInt16LittleEndian(h[10..], DosTime);
            BinaryPrimitives.WriteUInt16LittleEndian(h[12..], DosDate);
            BinaryPrimitives.WriteUInt32LittleEndian(h[14..], crcs[i]);
            BinaryPrimitives.WriteUInt32LittleEndian(h[18..], checked((uint)data.Length));
            BinaryPrimitives.WriteUInt32LittleEndian(h[22..], (uint)data.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(h[26..], checked((ushort)n.Length));
            BinaryPrimitives.WriteUInt16LittleEndian(h[28..], 0);         // extra length
            ms.Write(h[..30]);
            ms.Write(n);
            ms.Write(data);
        }
        long cdStart = ms.Position;
        for (int i = 0; i < entries.Count; i++)
        {
            (string name, byte[] data) = entries[i];
            byte[] n = Encoding.ASCII.GetBytes(name);
            h.Clear();
            BinaryPrimitives.WriteUInt32LittleEndian(h, 0x02014b50);
            BinaryPrimitives.WriteUInt16LittleEndian(h[4..], 20);         // version made by: 2.0, MS-DOS (fixed, not the host)
            BinaryPrimitives.WriteUInt16LittleEndian(h[6..], 20);         // version needed
            BinaryPrimitives.WriteUInt16LittleEndian(h[8..], 0);
            BinaryPrimitives.WriteUInt16LittleEndian(h[10..], 0);
            BinaryPrimitives.WriteUInt16LittleEndian(h[12..], DosTime);
            BinaryPrimitives.WriteUInt16LittleEndian(h[14..], DosDate);
            BinaryPrimitives.WriteUInt32LittleEndian(h[16..], crcs[i]);
            BinaryPrimitives.WriteUInt32LittleEndian(h[20..], (uint)data.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(h[24..], (uint)data.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(h[28..], (ushort)n.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(h[30..], 0);         // extra
            BinaryPrimitives.WriteUInt16LittleEndian(h[32..], 0);         // comment
            BinaryPrimitives.WriteUInt16LittleEndian(h[34..], 0);         // disk
            BinaryPrimitives.WriteUInt16LittleEndian(h[36..], 0);         // internal attributes
            BinaryPrimitives.WriteUInt32LittleEndian(h[38..], 0);         // external attributes
            BinaryPrimitives.WriteUInt32LittleEndian(h[42..], checked((uint)offsets[i]));
            ms.Write(h);
            ms.Write(n);
        }
        long cdEnd = ms.Position;
        Span<byte> e = stackalloc byte[22];
        BinaryPrimitives.WriteUInt32LittleEndian(e, 0x06054b50);
        BinaryPrimitives.WriteUInt16LittleEndian(e[4..], 0);
        BinaryPrimitives.WriteUInt16LittleEndian(e[6..], 0);
        BinaryPrimitives.WriteUInt16LittleEndian(e[8..], checked((ushort)entries.Count));
        BinaryPrimitives.WriteUInt16LittleEndian(e[10..], (ushort)entries.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(e[12..], checked((uint)(cdEnd - cdStart)));
        BinaryPrimitives.WriteUInt32LittleEndian(e[16..], checked((uint)cdStart));
        BinaryPrimitives.WriteUInt16LittleEndian(e[20..], 0);
        ms.Write(e);
        return ms.ToArray();
    }

    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var t = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[n] = c;
        }
        return t;
    }

    /// <summary>CRC-32 (IEEE 802.3, the ZIP and PNG polynomial).</summary>
    public static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint c = 0xFFFFFFFFu;
        foreach (byte b in data) c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}
