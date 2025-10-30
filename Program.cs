namespace hw21analyze
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    class Program
    {
        static async Task Main(string[] args)
        {
            var reportFolder = @"C:\work\HW21\ReportAssembly"; // 帳票クラスのフォルダ
            var webFolder = @"C:\work\HW21\hw21plus";    // .aspx.cs のフォルダ

            var exeFolder = AppContext.BaseDirectory;

            // BaseReport派生クラスの抽出結果ファイル
            var reportOutputPath = Path.Combine(exeFolder, "BaseReportClasses.txt");

            // BaseReport派生クラスのインスタンス生成を行っているaspx.csの結果ファイル
            var usageOutputPath = Path.Combine(exeFolder, "ReportUsageInAspxCs.txt");

            var reportClassNames = new List<(string ClassName, string FilePath)>();

            // ① 帳票クラスの抽出
            var csFiles = Directory.GetFiles(reportFolder, "*.cs", SearchOption.AllDirectories)
                                   .Where(f => !f.EndsWith(".aspx.cs", StringComparison.OrdinalIgnoreCase))
                                   .ToList();

            foreach (var file in csFiles)
            {
                var code = await File.ReadAllTextAsync(file);
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = await tree.GetRootAsync();

                var classNodes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

                foreach (var classNode in classNodes)
                {
                    // 基底クラスがBaseReport
                    var baseType = classNode.BaseList?.Types.FirstOrDefault()?.ToString();
                    if (baseType != null && baseType.EndsWith("BaseReport"))
                    {
                        // InitializeComponentが呼び出されている
                        var hasInit = classNode.DescendantNodes()
                            .OfType<InvocationExpressionSyntax>()
                            .Any(inv => inv.Expression.ToString().Contains("InitializeComponent"));

                        if (hasInit)
                        {
                            var className = classNode.Identifier.Text;
                            reportClassNames.Add((className, file));
                            Console.WriteLine($"◎ 帳票クラス検出: {className}（{file}）");
                        }
                    }
                }
            }

            // 出力①：帳票クラス一覧
            var reportLines = reportClassNames
                .Select(r => $"{r.ClassName}\t{r.FilePath}")
                .ToList();
            await File.WriteAllLinesAsync(reportOutputPath, reportLines, System.Text.Encoding.UTF8);
            Console.WriteLine($"✅ 帳票クラス一覧出力: {reportOutputPath}");

            // ② .aspx.cs ファイルでの使用箇所を検索
            var usageResults = new List<(string ClassName, string FilePath)>();
            var aspxFiles = Directory.GetFiles(webFolder, "*.aspx.cs", SearchOption.AllDirectories).ToList();

            foreach (var file in aspxFiles)
            {
                var code = await File.ReadAllTextAsync(file);
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = await tree.GetRootAsync();

                foreach (var (className, _) in reportClassNames)
                {
                    var found = root.DescendantNodes()
                        .OfType<ObjectCreationExpressionSyntax>()
                        .Any(n => n.Type.ToString().EndsWith(className));

                    if (found)
                    {
                        usageResults.Add((className, file));
                        Console.WriteLine($"▶ 使用検出: {className} → {file}");
                    }
                }
            }

            // 出力②：使用箇所一覧
            var usageLines = usageResults
                .Select(u => $"{u.ClassName}\t{u.FilePath}")
                .ToList();
            await File.WriteAllLinesAsync(usageOutputPath, usageLines, System.Text.Encoding.UTF8);
            Console.WriteLine($"✅ 使用箇所一覧出力: {usageOutputPath}");

            Console.WriteLine("🌈 解析完了！");
        }
    }
}
