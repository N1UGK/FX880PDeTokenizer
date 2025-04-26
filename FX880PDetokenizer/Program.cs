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

            if (!File.Exists(_opts.InputFile))
            {
                Console.WriteLine($"Input file {_opts.InputFile} was not found.");

                return 2;
            }

            int address = 0;

            try
            {
                address = Int32.Parse(_opts.Address.Replace("0x", string.Empty).Replace("0X", string.Empty), System.Globalization.NumberStyles.HexNumber);
            }
            catch
            {
                Console.WriteLine($"Address {_opts.Address} was not in hex or integer format.");

                return 3;
            }

            DeTokenizer d;

            d = new DeTokenizer(File.ReadAllBytes(_opts.InputFile), address);

            d.DeTokenize();

            if( d.HasProgramAreas )
            {
                string filenameBase = Path.GetFileNameWithoutExtension(_opts.OutputFile);
                string filenameExt = Path.GetExtension(_opts.OutputFile);
                string filenameBaseDir = Path.GetDirectoryName(_opts.OutputFile);

                foreach ( ProgramArea pa in d.programAreas.Where(a=>a.source != string.Empty))
                {
                    File.WriteAllText(Path.Combine(filenameBaseDir, filenameBase + pa.ProgramNumber + filenameExt), pa.source);
                }
            }
            else
            {
                File.WriteAllText(_opts.OutputFile, d.GetAllSources());
            }

            return 0;
        }

        class Options
        {
            [Option('i', "input", Required = true, HelpText = "Input file to be disassembled.")]
            public string InputFile { get; set; }

            [Option('o', "output", Required = true, HelpText = "Output file for disassembly.")]
            public string OutputFile { get; set; }

            [Option('a', "address", Default = "0000", Required = false, HelpText = "The optional starting address can be specified as a hexadecimal number without any prefixes. If omitted, a default value 0000 is assumed.")]
            public string Address { get; set; }
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
