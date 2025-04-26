using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FX880PDeTokenizer
{
    /*
     * Due to lack of documentation (that I was able to find), all rules below are guesses, and via reverse engineering, there could be errors and omissions!
     * 
     * Initial detokenizing is done by scanning for BASIC lines, not programs per se.  For example, many lines of BASIC might still be intact, but old fragments of deleted programs,
     * edited (older versions) of programs, and partial programs.  This, of course, is included along with fully intact programs it finds.
     * 
     * A BASIC line starts with a count byte, and ends (is terminated) by a zero (or null character).  The basic premise is, assume each byte is a count byte, verify, and if not, move to the 
     * next byte and repeat.  There is also an assumption here that the line number follows the start byte, and is two bytes.  Then, there is always a 0x20 (space).  Incorporating this expected
     * pattern into the search logic for a valid BASIC line further reduces false matches.  Further, the line number should be > 0 and the byte count for the line must be > 0.
     * 
     * If at the end of the current line we see another null (0x00), then that indicates the end of the program.
     * 
     */

    class DeTokenizer
    {
        private StringBuilder _sbOutput;

        public List<string> sources;
        public List<string> sourcesDiscarded;
        public List<ProgramArea> programAreas;

        private byte[] _source;

        private int _position = 0;

        Log _log = new Log();

        #region constants

        private const string NEWLINE = "\r\n";
        private const char SPACER = ' ';

        #endregion

        public DeTokenizer(byte[] source, int position)
        {
            _source = source;

            ClearSources();

            if ( _position < source.Length )
            {
                _position = position;
            }

            if (_position == 0)
            {
                LoadProgramAreas(_source);
            }
        }

        private void ClearSources()
        {
            sources = new List<string>();
            sourcesDiscarded = new List<string>();
        }

        private void LoadProgramAreas(byte[] source)
        {
            _log.logger.Info($"Looking for Program Areas...");

            programAreas = new List<ProgramArea>();

            //we could have a smaller image than a full 64KB image
            int endOfImage = source.Length - 1;

            int offset = endOfImage - ProgramArea.SIZE * 10 + 1;

            while(offset < endOfImage)
            {
                ProgramArea pa = new ProgramArea(source, offset);

                if (pa.ProgramSize > 0)
                {
                    programAreas.Add(pa);
                }

                offset += ProgramArea.SIZE;
            }

            _log.logger.Info($"Found {programAreas.Count} Program Areas.");

            foreach( ProgramArea pa in programAreas )
            {
                _log.logger.Info($"Program Area P{pa.ProgramNumber}: {(pa.ProgramSize > 1 ? pa.ProgramSize + " bytes from " + pa.StartAddress.ToString("X") + " to " + pa.EndAddress.ToString("X") : "(Unused)")}.");
            }
        }

        public void DeTokenize()
        {
            if (HasProgramAreas)
            {
                foreach( ProgramArea pa in programAreas.Where(a=>a.ProgramSize > 1))
                {
                    _log.logger.Info($"DeTokenizing P{pa.ProgramNumber}...");

                    DeTokenize(pa.StartAddress, pa.EndAddress);

                    pa.source = GetAllSources();

                    ClearSources();

                    _log.logger.Info($"DeTokenizing P{pa.ProgramNumber} completed.");
                }
            }
            else
            {
                DeTokenize(_position, _source.Length - 1);
            }
        }

        public bool HasProgramAreas
        {
            get
            {
                return programAreas.Count == 10;
            }
        }

        public void DeTokenize(int start, int end)
        {
            _position = start;

            int lineNumber;
            int previousLineNumber = 0;

            int lineBytesRemaining;

            byte b1,b2; //single bytes

            _sbOutput = new StringBuilder();

            int programStartPosition;

            while (_position < _source.Length && _position <= end )
            {
                ReadNext(out b1, out b2);

                if (_position + 3 >= _source.Length || _position + b1 >= _source.Length)
                {
                    _position++;

                    continue;
                }

                //start of a line is the "count" of bytes for that line. look forward by this count and if we have zero, thats the end of the line
                //this is likely the start of a valid BASIC line
                //also check that the byte right after the line number bytes is 0x20
                if (_source[_position + b1] == C_LINE_END && _source[_position + 3] == 0x20 && b1 > 0)
                {
                    if (_sbOutput.Length == 0)
                    {
                        programStartPosition = _position;
                    }

                    lineBytesRemaining = b1 + 1;

                    //line number is next two bytes, followed by a space (0x20)
                    lineNumber = ReadTwo(_source, _position + 1);

                    if( lineNumber <= previousLineNumber || lineNumber == 0 )
                    {
                        if (_sbOutput.Length > 0)
                        {
                            //last program was a partial, discard it
                            sourcesDiscarded.Add(_sbOutput.ToString());

                            _sbOutput.Clear();
                        }

                        _position++;

                        previousLineNumber = 0;

                        continue;
                    }

                    previousLineNumber = lineNumber;

                    _sbOutput.Append($"{lineNumber} ");

                    _position += 4;

                    lineBytesRemaining -= 4;

                    while (lineBytesRemaining > 0)
                    {
                        ReadNext(out b1, out b2);

                        if (b1 == DB_BASIC_GROUP_4)
                        {
                            #region Group 4

                            //command
                            switch (b2)
                            {
                                case B_REM:

                                    _sbOutput.Append($"REM");

                                    break;

                                case B_GOSUB:

                                    _sbOutput.Append($"GOSUB");

                                    break;

                                case B_RETURN:

                                    _sbOutput.Append($"RETURN");

                                    break;

                                case B_GOTO:

                                    _sbOutput.Append($"GOTO");

                                    break;

                                case B_FOR:

                                    _sbOutput.Append($"FOR");

                                    break;

                                case B_NEXT:

                                    _sbOutput.Append($"NEXT");

                                    break;

                                case B_IF:

                                    _sbOutput.Append($"IF");

                                    break;

                                case B_THEN:

                                    _sbOutput.Append($"THEN");

                                    break;

                                case B_EDIT:

                                    _sbOutput.Append($"EDIT");

                                    break;

                                case B_CLEAR:

                                    _sbOutput.Append($"CLEAR");

                                    break;

                                case B_OPEN:

                                    _sbOutput.Append($"OPEN");

                                    break;

                                case B_DEFSEG:

                                    _sbOutput.Append($"DEFSEG");

                                    break;

                                case B_PRINT:

                                    _sbOutput.Append($"PRINT");

                                    break;

                                case B_CLOSE:

                                    _sbOutput.Append($"CLOSE");

                                    break;

                                case B_PASS:

                                    _sbOutput.Append($"PASS");

                                    break;

                                case B_TRON:

                                    _sbOutput.Append($"TRON");

                                    break;

                                case B_TROFF:

                                    _sbOutput.Append($"TROFF");

                                    break;

                                case B_END:

                                    _sbOutput.Append($"END");

                                    break;

                                case B_STOP:

                                    _sbOutput.Append($"STOP");

                                    break;

                                case B_ON:

                                    _sbOutput.Append($"ON");

                                    break;

                                case B_LET:

                                    _sbOutput.Append($"LET");

                                    break;

                                case B_DATA:

                                    _sbOutput.Append($"DATA");

                                    break;

                                case B_READ:

                                    _sbOutput.Append($"READ");

                                    break;

                                case B_RESTORE:

                                    _sbOutput.Append($"RESTORE");

                                    break;

                                case B_LOCATE:

                                    _sbOutput.Append($"LOCATE");

                                    break;

                                case B_CLS:

                                    _sbOutput.Append($"CLS");

                                    break;

                                case B_SET:

                                    _sbOutput.Append($"SET");

                                    break;

                                case B_BEEP:

                                    _sbOutput.Append($"BEEP");

                                    break;

                                case B_DIM:

                                    _sbOutput.Append($"DIM");

                                    break;

                                case B_ERASE:

                                    _sbOutput.Append($"ERASE");

                                    break;

                                case B_POKE:

                                    _sbOutput.Append($"POKE");

                                    break;

                                case B_ERROR:

                                    _sbOutput.Append($"ERROR");

                                    break;

                                case B_RESUME:

                                    _sbOutput.Append($"RESUME");

                                    break;

                                default:

                                    //unknown command

                                    break;
                            }

                            _position += 2;

                            lineBytesRemaining -= 2;

                            #endregion 
                        }
                        else if (b1 == DB_BASIC_GROUP_7)
                        {
                            #region Group 7

                            //command
                            switch (b2)
                            {
                                case B_AS:

                                    _sbOutput.Append($"AS");

                                    break;

                                case B_THEN:

                                    _sbOutput.Append($"THEN");

                                    break;

                                case B_GOTO:

                                    _sbOutput.Append($"GOTO");

                                    break;

                                case B_ELSE:

                                    _sbOutput.Append($"ELSE");

                                    break;

                                case B_TO:

                                    _sbOutput.Append($"TO");

                                    break;

                                case B_STEP:

                                    _sbOutput.Append($"STEP");

                                    break;

                                case B_TAB:

                                    _sbOutput.Append($"TAB");

                                    break;

                                default:

                                    break;
                            }

                            _position += 2;

                            lineBytesRemaining -= 2;

                            #endregion
                        }
                        else if (b1 == DB_BASIC_GROUP_5)
                        {
                            #region Group 5

                            //command
                            switch (b2)
                            {
                                case B_PEEK:

                                    _sbOutput.Append($"PEEK");

                                    break;

                                case B_FRE:

                                    _sbOutput.Append($"FRE");

                                    break;

                                case B_ERL:

                                    _sbOutput.Append($"ERL");

                                    break;

                                default:

                                    break;
                            }

                            _position += 2;

                            lineBytesRemaining -= 2;

                            #endregion
                        }
                        else if (b1 == DB_BASIC_GROUP_6)
                        {
                            #region Group 6

                            //command
                            switch (b2)
                            {
                                case B_CHR:

                                    _sbOutput.Append($"CHR$");

                                    break;

                                case B_INPUT:

                                    _sbOutput.Append($"INPUT");

                                    break;

                                case B_INKEY:

                                    _sbOutput.Append($"INKEY");

                                    break;

                                default:

                                    break;
                            }

                            _position += 2;

                            lineBytesRemaining -= 2;

                            #endregion
                        }
                        else if (b1 == DB_BASIC_GROUP_2)
                        {
                            #region Group 2

                            //command
                            switch (b2)
                            {
                                case B_REM2:

                                    _sbOutput.Append($"'");

                                    break;

                                default:

                                    break;
                            }

                            //are these all 1 byte commands?
                            _position += 1;

                            lineBytesRemaining -= 1;

                            #endregion
                        }
                        else if (b1 == DB_BASIC_JP)
                        {
                            //line number reference
                            _sbOutput.Append($"{ReadTwo(_source, _position + 1)}");

                            _position += 3;

                            lineBytesRemaining -= 3;
                        }
                        else
                        {
                            //all other bytes, most of which would be literal strings, operands, or characters directly displayed on the LCD

                            switch (b1)
                            {
                                case C_LINE_END:

                                    _sbOutput.Append($"{NEWLINE}");

                                    break;

                                case C_SPACE:

                                    _sbOutput.Append($" ");

                                    break;

                                case C_MULTI_STATEMENT_SEPARATOR:

                                    _sbOutput.Append($":");

                                    break;

                                default:

                                    if (b1 > 0x20 && b1 < 0x7E)
                                    {
                                        _sbOutput.Append($"{(char)b1}");
                                    }
                                    else
                                    {

                                    }

                                    break;
                            }

                            _position++;

                            lineBytesRemaining--;
                        }
                    }
                }
                else
                {
                    switch(b1)
                    {
                        case C_PROGRAM_END:

                            if (_sbOutput.Length > 0)
                            {
                                sources.Add(_sbOutput.ToString());

                                _sbOutput.Clear();
                            }

                            break;
                    }
                                        
                    _position++;
                }
            }

            //residual - discard it
            if( _sbOutput.Length > 0)
            {
                sourcesDiscarded.Add(_sbOutput.ToString());

                _sbOutput.Clear();
            }
        }

        public string GetAllSources()
        {
            return String.Join($"{NEWLINE}{NEWLINE}", sources.ToArray());
        }

        private void ReadNext(out byte b1, out byte b2)
        {
            b2 = 0;

            b1 = _source[_position];

            if (_position + 1 < _source.Length)
            {
                b2 = _source[_position + 1];
            }
        }

        public static int ReadTwo(byte[] source, int position)
        {
            //Reads two bytes and returns an int - first byte is the position specified
            //second byte is the next byte (position + 1) - for Casio pockets, the second byte is the 
            //higher byte (reversed).

            int val = 0;
            
            if (position + 1 < source.Length)
            {
                val = source[position + 1] * 256 + source[position];
            }

            return val;
        }

        public string GetOutput()
        {
            return _sbOutput.ToString();
        }

        //tokens appear to be grouped, first byte in the token is the group, second byte is the command or function
        //my naming of the groups has no meaning (DB_BASIC_1, DB_BASIC_2, etc) nor order.

        private const byte DB_BASIC_GROUP_2 = 0x02;
        private const byte B_REM2 = 0x45;

        private const byte DB_BASIC_GROUP_4 = 0x04;
        private const byte B_GOSUB = 0x4A;
        private const byte B_RETURN = 0x4B;
        private const byte B_RESUME = 0x4C;
        private const byte B_RESTORE = 0x4D;
        private const byte B_PASS = 0x53;
        private const byte B_EDIT = 0x57;
        private const byte B_TRON = 0x5D;
        private const byte B_TROFF = 0x5F;
        private const byte B_POKE = 0x63;
        private const byte B_CLEAR = 0x6A;
        private const byte B_BEEP = 0x70;
        private const byte B_CLS = 0x71;
        private const byte B_CLOSE = 0x72;
        private const byte B_DEFSEG = 0x78;
        private const byte B_DIM = 0x7C;
        private const byte B_DATA = 0x80;
        private const byte B_FOR = 0x81;
        private const byte B_NEXT = 0x82;
        private const byte B_ERASE = 0x85;
        private const byte B_ERROR = 0x86;
        private const byte B_END = 0x87;
        private const byte B_IF = 0x8D;
        private const byte B_LET = 0x8F;
        private const byte B_LOCATE = 0x91;
        private const byte B_OPEN = 0x97;
        private const byte B_ON = 0x9A;
        private const byte B_PRINT = 0xA3;
        private const byte B_READ = 0xA8;
        private const byte B_REM = 0xA9;
        private const byte B_SET = 0xAC;
        private const byte B_STOP = 0xAE;

        private const byte DB_BASIC_GROUP_5 = 0x05;
        private const byte B_ERL = 0x4F;
        private const byte B_PEEK = 0x86;
        private const byte B_FRE = 0x8D;

        private const byte DB_BASIC_GROUP_6 = 0x06;
        private const byte B_INPUT = 0x9B;
        private const byte B_CHR = 0xA0;
        private const byte B_INKEY = 0xA8;

        private const byte DB_BASIC_GROUP_7 = 0x07;
        private const byte B_THEN = 0x47;
        private const byte B_ELSE = 0x48;
        private const byte B_GOTO = 0x49;
        private const byte B_TAB = 0xB6;
        private const byte B_AS = 0xBC;
        private const byte B_STEP = 0xC0;
        private const byte B_TO = 0xC1;

        private const byte DB_BASIC_JP = 0x03;

        //other control bytes

        private const byte C_LINE_END = 0x00;
        private const byte C_PROGRAM_END = 0x00;
        private const byte C_SPACE = 0x20;
        private const byte C_MULTI_STATEMENT_SEPARATOR = 0x01; //displayed as a colon to the user on the pocket computer
    }
  
    class ProgramArea
    {
        /* Start Address (2 bytes)
         * 0x00
         * End Address (2 bytes)
         * 0x00
         * 0x80
         * 0x50 "P"
         * ASCII 0-9 for program area #
         * 0x20 0x20 0x20 0x20 0x20 0x20 (6 spaces)
         */

        private const int OFFSET_START_ADDRESS = 0;
        private const int OFFSET_END_ADDRESS = 3;
        private const int OFFSET_P = 7;
        private const int OFFSET_PNUM = 8;

        public const int SIZE = 15;

        public int ProgramNumber = -1;
        public int StartAddress = 0;
        public int EndAddress = 0;
        public string source = string.Empty;

        public int ProgramSize
        {
            get
            {
                return ProgramNumber == -1 ? 0 : EndAddress - StartAddress;
            }
        }

        public ProgramArea(byte[] source, int offset)
        {
            if (source[offset + OFFSET_P] == 0x50 && source[offset + OFFSET_PNUM] >= 0x30 && source[offset + OFFSET_PNUM] <= 0x39)
            {
                ProgramNumber = (int)source[offset + OFFSET_PNUM] - 48;

                StartAddress = DeTokenizer.ReadTwo(source, offset + OFFSET_START_ADDRESS);
                EndAddress = DeTokenizer.ReadTwo(source, offset + OFFSET_END_ADDRESS);
            }
        }
    }
}
