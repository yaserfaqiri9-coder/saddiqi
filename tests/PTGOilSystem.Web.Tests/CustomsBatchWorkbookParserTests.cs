using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PTGOilSystem.Web.Helpers;
using PTGOilSystem.Web.Models.Entities;
using Xunit;

namespace PTGOilSystem.Web.Tests;

// اطمینان از اینکه پارسر فایل گروهی گمرک همهٔ ستون‌های عکسِ کاربر را — با وجود
// جابه‌جایی کلمات، کلمهٔ اضافه («فی»/«۵۰۰») و «وزن موتر» به‌جای «وزن سیمیر» —
// شناسایی و درست جمع می‌کند، و ستون ترکیبی حق‌الخدمه+۲۰پول را دوبار نمی‌شمارد.
public class CustomsBatchWorkbookParserTests
{
    [Fact]
    public void Parse_Recognizes_All_Image_Columns_With_Correct_Amounts()
    {
        var bytes = BuildWorkbook(
            headers: new[]
            {
                ("A", "تاریخ"),
                ("B", "نمبر موتر"),
                ("C", "وزن موتر"),
                ("D", "مصارف محصولی"),
                ("E", "مصارف فواید عامه"),
                ("F", "کمیشن بانک"),
                ("G", "نورم استندرد"),
                ("H", "حق الخدمه مواد نفت"),
                ("I", "مصرف 20 پول"),
                ("J", "مصارف خط آهن"),
                ("K", "بارنامه فی واگن 500"),
                ("L", "ترازوی موتر"),
                ("M", "کمیشن بارچلان")
            },
            data: new[]
            {
                ("A", "2026-07-01"),
                ("B", "12345"),
                ("C", "25"),
                ("D", "100"),
                ("E", "200"),
                ("F", "300"),
                ("G", "400"),
                ("H", "500"),
                ("I", "600"),
                ("J", "700"),
                ("K", "800"),
                ("L", "900"),
                ("M", "1000")
            });

        using var stream = new MemoryStream(bytes);
        var rows = CustomsBatchWorkbookParser.Parse(stream);

        var row = Assert.Single(rows);
        Assert.Equal("12345", row.PlateNumber);
        Assert.Equal(25m, row.WeightMt);

        decimal AmountOf(CustomsComponentType t) =>
            row.Amounts.Where(a => a.ComponentType == t).Sum(a => a.Amount);

        Assert.Equal(100m, AmountOf(CustomsComponentType.Mahsooli));
        Assert.Equal(200m, AmountOf(CustomsComponentType.FawaidAama));
        Assert.Equal(300m, AmountOf(CustomsComponentType.KomisionBank));
        Assert.Equal(400m, AmountOf(CustomsComponentType.NormStandard));
        Assert.Equal(500m, AmountOf(CustomsComponentType.HaqKhidma));
        Assert.Equal(600m, AmountOf(CustomsComponentType.Masraf20Pul));
        Assert.Equal(700m, AmountOf(CustomsComponentType.KhatAhan));
        Assert.Equal(800m, AmountOf(CustomsComponentType.BarnamaWagon));
        Assert.Equal(900m, AmountOf(CustomsComponentType.TarazuMotor));
        Assert.Equal(1000m, AmountOf(CustomsComponentType.KomisionBarchalani));

        // ۱۰ جزء مبلغ، بدون تکرار/از‌قلم‌افتادن.
        Assert.Equal(10, row.Amounts.Count);
        Assert.Equal(5500m, row.Amounts.Sum(a => a.Amount));
    }

    [Fact]
    public void Parse_Combined_HaqKhidma_And_20Pul_Column_Is_Counted_Once()
    {
        var bytes = BuildWorkbook(
            headers: new[]
            {
                ("A", "نمبر موتر"),
                ("B", "مصارف محصولی"),
                ("C", "حق الخذمه مواد نفت و کمیشن 20 پول")
            },
            data: new[]
            {
                ("A", "999"),
                ("B", "70000"),
                ("C", "12000")
            });

        using var stream = new MemoryStream(bytes);
        var rows = CustomsBatchWorkbookParser.Parse(stream);

        var row = Assert.Single(rows);
        Assert.Equal(2, row.Amounts.Count);
        Assert.Equal(12000m, row.Amounts.Single(a => a.ComponentType == CustomsComponentType.HaqKhidma).Amount);
        Assert.DoesNotContain(row.Amounts, a => a.ComponentType == CustomsComponentType.Masraf20Pul);
    }

    [Fact]
    public void Parse_WeightSimir_Column_Is_Weight_Not_Simir_Key()
    {
        // فایلِ «کرایه موترها» فقط ستون «وزن سیمیر» دارد (سیمیرِ جدا ندارد)؛ این ستون
        // باید وزن خوانده شود، نه کلیدِ سیمیر. کلید = نمبر موتر.
        var bytes = BuildWorkbook(
            headers: new[]
            {
                ("A", "نمبر موتر"),
                ("B", "وزن سیمیر"),
                ("C", "مصارف محصولی")
            },
            data: new[]
            {
                ("A", "45917"),
                ("B", "33.300"),
                ("C", "164621.547")
            });

        using var stream = new MemoryStream(bytes);
        var row = Assert.Single(CustomsBatchWorkbookParser.Parse(stream));

        Assert.Equal("45917", row.PlateNumber);
        Assert.Null(row.Simir);
        Assert.Equal(33.300m, row.WeightMt);
        Assert.Equal(164621.547m, row.Amounts.Single(a => a.ComponentType == CustomsComponentType.Mahsooli).Amount);
    }

    private static byte[] BuildWorkbook((string Col, string Val)[] headers, (string Col, string Val)[] data)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(new SheetData(
                BuildRow(1, headers),
                BuildRow(2, data)));

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "customs"
            });

            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    private static Row BuildRow(uint rowIndex, (string Col, string Val)[] cells)
    {
        var row = new Row { RowIndex = rowIndex };
        foreach (var (col, val) in cells)
        {
            row.Append(new Cell
            {
                CellReference = col + rowIndex,
                DataType = CellValues.String,
                CellValue = new CellValue(val)
            });
        }

        return row;
    }
}
