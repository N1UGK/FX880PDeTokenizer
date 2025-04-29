using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FX880PDeTokenizer
{
    class Program
    {
        private static Options _opts;

        static int Main(string[] args)
        {
            ParserResult<Options> pResult = Parser.Default.ParseArguments<Options>(args).WithParsed(RunOptions).WithNotParsed(HandleParseError);

            if (_opts == null || pResult.Tag == ParserResultType.NotParsed)
            {
                HelpText hText = HelpText.AutoBuild(pResult);

                Console.Write(hText.ToString());

                return 1;
            }

            if(_opts.InputFiles == null || _opts.InputFiles.Length == 0 )
            {
                Console.WriteLine($"At least one input file is required.");
            }

            string[] inputFiles = _opts.InputFiles.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach ( string inputFile in inputFiles)
            {
                if (!File.Exists(inputFile))
                {
                    Console.WriteLine($"Input file {inputFile} was not found.");

                    return 2;
                }
            }

            int startAt = 0;

            try
            {
                startAt = Int32.Parse(_opts.StartAt.Replace("0x", string.Empty).Replace("0X", string.Empty), System.Globalization.NumberStyles.HexNumber);
            }
            catch
            {
                Console.WriteLine($"Address {_opts.StartAt} was not in hex or integer format.");

                return 3;
            }

            DeTokenizer d;

            byte[] allFilesSource;

            using (MemoryStream allFrameStream = new MemoryStream())
            {
                foreach (string inputFile in inputFiles)
                {
                    byte[] b = File.ReadAllBytes(inputFile);

                    allFrameStream.Write(b, 0, b.Length);
                }

                allFilesSource = allFrameStream.ToArray();
            }

            d = new DeTokenizer(allFilesSource, startAt);

            d.DeTokenize();

            string filenameBase = Path.GetFileNameWithoutExtension(_opts.OutputFile);
            string filenameExt = Path.GetExtension(_opts.OutputFile);
            string filenameBaseDir = Path.GetDirectoryName(_opts.OutputFile);

            if ( d.HasProgramAreas )
            {
                foreach ( ProgramArea pa in d.programAreas.Where(a=>a.source != string.Empty))
                {
                    File.WriteAllText(Path.Combine(filenameBaseDir, filenameBase + pa.ProgramNumber + filenameExt), pa.source, Encoding.UTF8);
                }
            }
            else
            {
                File.WriteAllText(_opts.OutputFile, d.GetAllSources(), Encoding.UTF8);

                if( d.functionMappingEntries.Any())
                {
                    StringBuilder sb = new StringBuilder();

                    foreach (LibFunctionMapEntry fme in d.functionMappingEntries)
                    {
                        sb.Append($"{fme.libraryNumber}: {(fme.StartAddress + (fme.StartAddressSegment - 4) * 0x10000).ToString("X8")}-{(fme.EndAddress+(fme.EndAddressSegment - 4)*0x10000).ToString("X8")}\r\n");
                    }

                    File.WriteAllText(Path.Combine(filenameBaseDir, filenameBase + ".libmap" + filenameExt), sb.ToString(), Encoding.UTF8);
                }
            }

            return 0;
        }

        class Options
        {
            [Option('i', "input", Required = true, HelpText = "Input file(s) to be disassembled, separate multiple files with a comma.")]
            public string InputFiles { get; set; }
 
            [Option('o', "output", Required = true, HelpText = "Output file for disassembly.")]
            public string OutputFile { get; set; }

            [Option('a', "startat", Default = "0000", Required = false, HelpText = "The optional start position can be specified as a hexadecimal number without any prefixes. If omitted, a default value 0000 is assumed.")]
            public string StartAt { get; set; }
        }

        static void RunOptions(Options opts)
        {
            //handle options
            _opts = opts;
        }

        static void HandleParseError(IEnumerable<Error> errs)
        {
            //handle errors
        }
    }
}
