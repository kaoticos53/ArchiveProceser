using System.IO;
using FileFlow.Plugin.Documents;
using FileFlow.Sdk;
using Moq;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins;

public class DocumentsTests
{
    private string CreateSamplePdf(string filePath, string text, int pages = 1)
    {
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

        using var doc = new PdfDocument();
        for (int i = 0; i < pages; i++)
        {
            var page = doc.AddPage();
            using var gfx = XGraphics.FromPdfPage(page);
            var font = new XFont("Arial", 12);
            gfx.DrawString($"{text} - Page {i + 1}", font, XBrushes.Black, new XPoint(50, 50));
        }

        doc.Save(filePath);
        return filePath;
    }

    [Fact]
    public void PdfMergeNode_MergePdfFiles_CombinesDocumentsSuccessfully()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlowPdfMergeTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            string pdf1 = Path.Combine(tempDir, "doc1.pdf");
            string pdf2 = Path.Combine(tempDir, "doc2.pdf");
            string outputPdf = Path.Combine(tempDir, "merged.pdf");

            CreateSamplePdf(pdf1, "Document One", pages: 1);
            CreateSamplePdf(pdf2, "Document Two", pages: 2);

            PdfMergeNode.MergePdfFiles([pdf1, pdf2], outputPdf);

            Assert.True(File.Exists(outputPdf));

            using var mergedDoc = PdfSharp.Pdf.IO.PdfReader.Open(outputPdf, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
            Assert.Equal(3, mergedDoc.PageCount);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task PdfSplitNode_SplitsMultiplePagePdf()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlowPdfSplitTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            string pdf = Path.Combine(tempDir, "multipage.pdf");
            string outDir = Path.Combine(tempDir, "SplitOutput");
            CreateSamplePdf(pdf, "Split Test", pages: 3);

            var node = new PdfSplitNode();
            node.Parameters["OutputDirectory"] = outDir;

            var item = new FileItemContext(pdf);
            var emittedItems = new List<FileItemContext>();

            var mockContext = new Mock<FileFlow.Sdk.IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync(It.IsAny<string>(), It.IsAny<FileItemContext>()))
                .Callback<string, FileItemContext>((port, itm) =>
                {
                    if (port == "Out") emittedItems.Add(itm);
                })
                .Returns(Task.CompletedTask);

            await node.ExecuteAsync("In", item, mockContext.Object);

            Assert.Equal(3, emittedItems.Count);
            Assert.All(emittedItems, page => Assert.True(File.Exists(page.CurrentPath)));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task PdfMetadataNode_ReadsAndModifiesMetadata()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlowPdfMetaTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            string pdf = Path.Combine(tempDir, "meta.pdf");
            string outDir = Path.Combine(tempDir, "MetaOutput");
            CreateSamplePdf(pdf, "Meta Test", pages: 1);

            var node = new PdfMetadataNode();
            node.Parameters["UpdateMetadata"] = true;
            node.Parameters["Title"] = "FileFlow Report";
            node.Parameters["Author"] = "Antigravity Agent";
            node.Parameters["OutputDirectory"] = outDir;

            var item = new FileItemContext(pdf);
            FileItemContext? emitted = null;

            var mockContext = new Mock<FileFlow.Sdk.IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                .Callback<string, FileItemContext>((port, itm) => emitted = itm)
                .Returns(Task.CompletedTask);

            await node.ExecuteAsync("In", item, mockContext.Object);

            Assert.NotNull(emitted);
            Assert.True(File.Exists(emitted.CurrentPath));

            using var modifiedDoc = PdfSharp.Pdf.IO.PdfReader.Open(emitted.CurrentPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
            Assert.Equal("FileFlow Report", modifiedDoc.Info.Title);
            Assert.Equal("Antigravity Agent", modifiedDoc.Info.Author);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task PdfTextExtractorNode_ExtractsTextSuccessfully()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FileFlowPdfExtractTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            string pdf = Path.Combine(tempDir, "text.pdf");
            CreateSamplePdf(pdf, "Extracted Content Line", pages: 1);

            var node = new PdfTextExtractorNode();
            var item = new FileItemContext(pdf);

            var mockContext = new Mock<FileFlow.Sdk.IFlowExecutionContext>();
            mockContext.Setup(c => c.EmitAsync("Out", It.IsAny<FileItemContext>()))
                .Returns(Task.CompletedTask);

            await node.ExecuteAsync("In", item, mockContext.Object);

            Assert.True(item.Metadata.ContainsKey("PdfText"));
            Assert.Equal(1, item.Metadata["PdfPageCount"]);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
