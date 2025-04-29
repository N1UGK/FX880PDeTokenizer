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

            if (_position < source.Length)
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

            while (offset < endOfImage)
            {
                ProgramArea pa = new ProgramArea(source, offset);

                if (pa.ProgramSize > 0)
                {
                    programAreas.Add(pa);
                }

                offset += ProgramArea.SIZE;
            }

            _log.logger.Info($"Found {programAreas.Count} Program Areas.");

            foreach (ProgramArea pa in programAreas)
            {
                _log.logger.Info($"Program Area P{pa.ProgramNumber}: {(pa.ProgramSize > 1 ? pa.ProgramSize + " bytes from " + pa.StartAddress.ToString("X") + " to " + pa.EndAddress.ToString("X") : "(Unused)")}.");
            }
        }

        public bool HasProgramAreas
        {
            get
            {
                return programAreas.Count == 10;
            }
        }

        public void DeTokenize()
        {
            if (HasProgramAreas)
            {
                foreach (ProgramArea pa in programAreas.Where(a => a.ProgramSize > 1))
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

        public void DeTokenize(int start, int end)
        {
            _position = start;

            int lineNumber;
            int previousLineNumber = 0;

            int lineBytesRemaining;

            byte b1, b2, b3, b4; //single bytes

            byte previousb1 = 0;

            _sbOutput = new StringBuilder();

            int programStartPosition;

            while (_position < _source.Length && _position <= end)
            {
                ReadNext(_source, _position, out b1, out b2);

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

                    if (lineNumber <= previousLineNumber || lineNumber == 0)
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

                    _sbOutput.Append($"{lineNumber}{SPACER}");

                    _position += 4;

                    lineBytesRemaining -= 4;

                    while (lineBytesRemaining > 0)
                    {
                        ReadNext(_source, _position, out b1, out b2);

                        if (b1 == DB_BASIC_GROUP_4)
                        {
                            #region Group 4

                            AddSpaceBefore(previousb1, b1);

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

                                case B_ANGLE:

                                    _sbOutput.Append($"ANGLE");

                                    break;

                                case B_LLIST:

                                    _sbOutput.Append($"LLIST");

                                    break;

                                case B_LPRINT:

                                    _sbOutput.Append($"LPRINT");

                                    break;

                                case B_SAVE:

                                    _sbOutput.Append($"SAVE");

                                    break;

                                case B_LOAD:

                                    _sbOutput.Append($"LOAD");

                                    break;

                                case B_VERIFY:

                                    _sbOutput.Append($"VERIFY");

                                    break;

                                case B_NEW:

                                    _sbOutput.Append($"NEW");

                                    break;

                                case B_WRITE:

                                    _sbOutput.Append($"WRITE#");

                                    break;

                                case B_OUT:

                                    _sbOutput.Append($"OUT");

                                    break;

                                case B_DEF:

                                    //undocumented token
                                    _sbOutput.Append($"DEF");

                                    break;

                                case B_PUT:

                                    //undocumented token?
                                    _sbOutput.Append($"PUT");

                                    break;

                                case B_MODE:

                                    //undocumented token?
                                    _sbOutput.Append($"MODE");

                                    break;

                                case B_CALCJMP:

                                    //undocumented token?
                                    _sbOutput.Append($"CALCJMP");

                                    break;


                                default:

                                    //unknown token

                                    LogUnknownToken(lineNumber, b1, b2);

                                    OutputUnknownToken(b1, b2);

                                    break;
                            }

                            _position += 2;

                            lineBytesRemaining -= 2;

                            AddSpaceAfter(previousb1, _source[_position]);

                            #endregion 
                        }
                        else if (b1 == DB_BASIC_GROUP_7)
                        {
                            #region Group 7

                            AddSpaceBefore(previousb1, b1);

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

                                case B_NOT:

                                    _sbOutput.Append($"NOT");

                                    break;

                                case B_ALL:

                                    _sbOutput.Append($"ALL");

                                    break;

                                case B_OR:

                                    _sbOutput.Append($"OR");

                                    break;

                                case B_MOD:

                                    _sbOutput.Append($"MOD");

                                    break;

                                case B_AND:

                                    _sbOutput.Append($"AND");

                                    break;

                                case B_XOR:

                                    _sbOutput.Append($"XOR");

                                    break;

                                default:

                                    //unknown token

                                    LogUnknownToken(lineNumber, b1, b2);

                                    OutputUnknownToken(b1, b2);

                                    break;
                            }

                            _position += 2;

                            lineBytesRemaining -= 2;

                            AddSpaceAfter(previousb1, _source[_position]);

                            #endregion
                        }
                        else if (b1 == DB_BASIC_GROUP_5)
                        {
                            #region Group 5

                            AddSpaceBefore(previousb1, b1);

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

                                case B_ERR:

                                    _sbOutput.Append($"ERR");

                                    break;

                                case B_SIN:

                                    _sbOutput.Append($"SIN");

                                    break;

                                case B_COS:

                                    _sbOutput.Append($"COS");

                                    break;

                                case B_TAN:

                                    _sbOutput.Append($"TAN");

                                    break;

                                case B_ASN:

                                    _sbOutput.Append($"ASN");

                                    break;

                                case B_ACS:

                                    _sbOutput.Append($"ACS");

                                    break;

                                case B_ATN:

                                    _sbOutput.Append($"ATN");

                                    break;

                                case B_HYPSIN:

                                    _sbOutput.Append($"HYPSIN");

                                    break;

                                case B_HYPCOS:

                                    _sbOutput.Append($"HYPCOS");

                                    break;

                                case B_HYPTAN:

                                    _sbOutput.Append($"HYPTAN");

                                    break;

                                case B_HYPASN:

                                    _sbOutput.Append($"HYPASN");

                                    break;

                                case B_HYPACS:

                                    _sbOutput.Append($"HYPACS");

                                    break;

                                case B_HYPATN:

                                    _sbOutput.Append($"HYPATN");

                                    break;

                                case B_EXP:

                                    _sbOutput.Append($"EXP");

                                    break;

                                case B_LOG:

                                    _sbOutput.Append($"LOG");

                                    break;

                                case B_LN:

                                    _sbOutput.Append($"LN");

                                    break;

                                case B_SQR:

                                    _sbOutput.Append($"SQR");

                                    break;

                                case B_CUR:

                                    _sbOutput.Append($"CUR");

                                    break;

                                case B_ABS:

                                    _sbOutput.Append($"ABS");

                                    break;

                                case B_SGN:

                                    _sbOutput.Append($"SGN");

                                    break;

                                case B_INT:

                                    _sbOutput.Append($"INT");

                                    break;

                                case B_FIX:

                                    _sbOutput.Append($"FIX");

                                    break;

                                case B_FRAC:

                                    _sbOutput.Append($"FRAC");

                                    break;

                                case B_ROUND:

                                    _sbOutput.Append($"ROUND");

                                    break;

                                case B_RAN:

                                    _sbOutput.Append($"RAN#");

                                    break;

                                case B_PI:

                                    _sbOutput.Append($"PI");

                                    break;

                                case B_FACT:

                                    _sbOutput.Append($"FACT");

                                    break;

                                case B_NPR:

                                    _sbOutput.Append($"NPR");

                                    break;

                                case B_NCR:

                                    _sbOutput.Append($"NCR");

                                    break;

                                case B_POL:

                                    _sbOutput.Append($"POL");

                                    break;

                                case B_REC:

                                    _sbOutput.Append($"REC");

                                    break;

                                case B_ASC:

                                    _sbOutput.Append($"ASC");

                                    break;

                                case B_VAL:

                                    _sbOutput.Append($"VAL");

                                    break;

                                case B_VALF:

                                    _sbOutput.Append($"VALF");

                                    break;

                                case B_LEN:

                                    _sbOutput.Append($"LEN");

                                    break;

                                case B_DEG:

                                    _sbOutput.Append($"DEG");

                                    break;

                                case B_EOF:

                                    _sbOutput.Append($"EOF");

                                    break;

                                default:

                                    //unknown token

                                    LogUnknownToken(lineNumber, b1, b2);

                                    OutputUnknownToken(b1, b2);

                                    break;
                            }

                            _position += 2;

                            lineBytesRemaining -= 2;

                            AddSpaceAfter(previousb1, _source[_position]);

                            #endregion
                        }
                        else if (b1 == DB_BASIC_GROUP_6)
                        {
                            #region Group 6

                            AddSpaceBefore(previousb1, b1);

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

                                    _sbOutput.Append($"INKEY$");

                                    break;

                                case B_STR:

                                    _sbOutput.Append($"STR$");

                                    break;

                                case B_MID:

                                    _sbOutput.Append($"MID$");

                                    break;

                                case B_RIGHT:

                                    _sbOutput.Append($"RIGHT$");

                                    break;

                                case B_LEFT:

                                    _sbOutput.Append($"LEFT$");

                                    break;

                                case B_HEX:

                                    _sbOutput.Append($"HEX$");

                                    break;

                                case B_DMS:

                                    _sbOutput.Append($"DMS$");

                                    break;

                                case B_CALC:

                                    _sbOutput.Append($"CALC$");

                                    break;

                                default:

                                    //unknown token

                                    LogUnknownToken(lineNumber, b1, b2);

                                    OutputUnknownToken(b1, b2);

                                    break;
                            }

                            _position += 2;

                            lineBytesRemaining -= 2;

                            AddSpaceAfter(previousb1, _source[_position]);

                            #endregion
                        }
                        else if (b1 == DB_BASIC_GROUP_2)
                        {
                            #region Group 2

                            AddSpaceBefore(previousb1, b1);

                            //command
                            switch (b2)
                            {
                                case B_REM2:

                                    _sbOutput.Append($"'");

                                    break;

                                default:

                                    //unknown token

                                    LogUnknownToken(lineNumber, b1, b2);

                                    OutputUnknownToken(b1, b2);

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

                                    //This should not appear within a line, i.e. with lineBytesRemaining - if it does, likely not a valid BASIC line.
                                    if (lineBytesRemaining > 1)
                                    {
                                        _log.logger.Debug($"{_position.ToString("X")} line {lineNumber}: null byte found with bytes remaining in current line.  Invalid BASIC line.");

                                        previousLineNumber = 0;

                                        if (_sbOutput.Length > 0)
                                        {
                                            //last program was a partial, discard it
                                            sourcesDiscarded.Add(_sbOutput.ToString());

                                            _sbOutput.Clear();
                                        }
                                    }
                                    else
                                    {
                                        _sbOutput.Append($"{NEWLINE}");
                                    }

                                    break;

                                case C_SPACE:

                                    _sbOutput.Append($" ");

                                    break;

                                case C_MULTI_STATEMENT_SEPARATOR:

                                    //ELSE pattern seems to use a colon, then ELSE - display just the else in this case.
                                    //this would match the behavior seen on the editor on the pocket computer (user does not see the colon)
                                    //see if next token after any spaces is an ELSE.
                                    ReadNext(_source, _position + 1, out b3, out b4, true, C_SPACE);

                                    if (b3 == DB_BASIC_GROUP_7 && b4 == B_ELSE)
                                    {
                                        _log.logger.Debug($"{_position.ToString("X")} line {lineNumber}: multi-statement marker found before 'ELSE', ignored.");
                                    }
                                    else
                                    {
                                        _sbOutput.Append($":");
                                    }

                                    break;

                                default:

                                    string charConverted = Convert(b1);

                                    if (charConverted != CHAR_UNKNOWN)
                                    {
                                        _sbOutput.Append(charConverted);
                                    }
                                    else
                                    {
                                        //unknown byte

                                        _log.logger.Debug($"{_position.ToString("X")} line {lineNumber}: unknown byte {b1.ToString("X")}.");

                                        _sbOutput.Append($"{{{b1.ToString("X")}}}");
                                    }

                                    break;
                            }

                            _position++;

                            lineBytesRemaining--;
                        }

                        previousb1 = b1;
                    }
                }
                else
                {
                    switch (b1)
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
            if (_sbOutput.Length > 0)
            {
                sourcesDiscarded.Add(_sbOutput.ToString());

                _sbOutput.Clear();
            }
        }

        private void LogUnknownToken(int lineNumber, byte b1, byte b2)
        {
            _log.logger.Debug($"{_position.ToString("X")} line {lineNumber}: unknown token {b2.ToString("X")} {b1.ToString("X")}.");
        }

        private void OutputUnknownToken(byte b1, byte b2)
        {
            _sbOutput.Append($"{{{b2.ToString("X")} {b1.ToString("X")}}}");
        }

        private void AddSpaceAfter(byte previousByte, byte currentByte)
        {
            switch (currentByte)
            {
                case C_SPACE:
                case C_PAREN_OPEN:

                    return;

                case C_EQUALS:
                case C_MULTI_STATEMENT_SEPARATOR:
                case C_LINE_END:

                    if (IsToken(previousByte) == true)
                    {
                        _sbOutput.Append(SPACER);
                    }

                    return;

                default:

                    _sbOutput.Append(SPACER);

                    break;
            }
        }

        private void AddSpaceBefore(byte previousByte, byte currentByte)
        {
            switch (previousByte)
            {
                case C_LINE_END:
                case C_SPACE:
                case C_SEMICOLON:

                    return;

                default:

                    if (IsToken(previousByte) == false && IsAlphaNumeric(previousByte) == true && IsToken(currentByte) == true)
                    {
                        _sbOutput.Append(SPACER);
                    }

                    break;

            }
        }

        private bool IsLiteralChar(byte b)
        {
            return (b >= 0x20 && b <= 0x7E);
        }

        private bool IsAlphaNumeric(byte b)
        {
            return (b >= 0x30 && b <= 0x39) || (b >= 0x41 && b <= 0x5A) || (b >= 0x61 && b <= 0x7A);
        }

        private bool IsToken(byte b)
        {
            switch (b)
            {
                case DB_BASIC_GROUP_2:
                case DB_BASIC_GROUP_4:
                case DB_BASIC_GROUP_5:
                case DB_BASIC_GROUP_6:
                case DB_BASIC_GROUP_7:

                    return true;

                default:

                    return false;
            }
        }

        public const string CHAR_UNKNOWN = "UNKNOWN";

        public string Convert(byte b)
        {
            //https://en.wikipedia.org/wiki/Casio_calculator_character_sets
            //these are all strings due to 1 char in casio charset which requires two unicode chars to represent (0x9E)

            string[] casioToUnicode =
            {
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,

               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,

               " ",
               "!",
               "\"",
               "#",
               "$",
               "%",
               "&",
               "'",
               "(",
               ")",
               "*",
               "+",
               ",",
               "-",
               ".",
               "/",

               "0",
               "1",
               "2",
               "3",
               "4",
               "5",
               "6",
               "7",
               "8",
               "9",
               ":",
               ";",
               "<",
               "=",
               ">",
               "?",

               "@",
               "A",
               "B",
               "C",
               "D",
               "E",
               "F",
               "G",
               "H",
               "I",
               "J",
               "K",
               "L",
               "M",
               "N",
               "O",

               "P",
               "Q",
               "R",
               "S",
               "T",
               "U",
               "V",
               "W",
               "X",
               "Y",
               "Z",
               "[",
               "¥",
               "]",
               "^",
               "_",

               "`",
               "a",
               "b",
               "c",
               "d",
               "e",
               "f",
               "g",
               "h",
               "i",
               "j",
               "k",
               "l",
               "m",
               "n",
               "o",

               "p",
               "q",
               "r",
               "s",
               "t",
               "u",
               "v",
               "w",
               "x",
               "y",
               "z",
               "{",
               "|",
               "}",
               "~",
               " ",

               "\u212B",
               "\u222B",
               "\u221A",
               "\u00B4",
               "\u03A3",
               "\u03A9",
               "\u2592",
               "\u25AE",
               "\u03B1",
               "\u03B2",
               "\u03B3",
               "\u03B5",
               "\u03B8",
               "\u03BC",
               "\u03C3",
               "\u03A6",
         
               "\u2070",
               "\u00B9",
               "\u00B2",
               "\u00B3",
               "\u2074",
               "\u2075",
               "\u2076",
               "\u2077",
               "\u2078",
               "\u2079",
               "\u207A",
               "\u207B",
               "\u207F",
               "\u02E3",
               "⁻¹", //this one lines up and is more consistent with the others
               "\u00F7",            

               "\u00A0",
               "\uFF61",
               "\uFF62",
               "\uFF63",
               "\uFF64",
               "\uFF65",
               "\uFF66",
               "\uFF67",
               "\uFF68",
               "\uFF69",
               "\uFF6A",
               "\uFF6B",
               "\uFF6C",
               "\uFF6D",
               "\uFF6E",
               "\uFF6F",              

               "\uFF70",
               "\uFF71",
               "\uFF72",
               "\uFF73",
               "\uFF74",
               "\uFF75",
               "\uFF76",
               "\uFF77",
               "\uFF78",
               "\uFF79",
               "\uFF7A",
               "\uFF7B",
               "\uFF7C",
               "\uFF7D",
               "\uFF7E",
               "\uFF7F",

               "\uFF80",
               "\uFF81",
               "\uFF82",
               "\uFF83",
               "\uFF84",
               "\uFF85",
               "\uFF86",
               "\uFF87",
               "\uFF88",
               "\uFF89",
               "\uFF8A",
               "\uFF8B",
               "\uFF8C",
               "\uFF8D",
               "\uFF8E",
               "\uFF8F",

               "\uFF90",
               "\uFF91",
               "\uFF92",
               "\uFF93",
               "\uFF94",
               "\uFF95",
               "\uFF96",
               "\uFF97",
               "\uFF98",
               "\uFF99",
               "\uFF9A",
               "\uFF9B",
               "\uFF9C",
               "\uFF9D",
               "\uFF9E",
               "\uFF9F",

               "\u2265",
               "\u2264",
               "\u2260",
               "\u2191",
               "\u2190",
               "\u2193",
               "\u2192",
               "\u03C0",
               "\u2260",
               "\u2265",
               "\u2666",
               "\u2663",
               "\u25A1",
               "\u25CB",
               "\u25B3",
               "\u005C",

               "\u00D7",
               "\u5186",
               "\u5E74",
               "\u6708",
               "\u65E5",
               "\u5343",
               "\u4E07",
               "\u00A3",
               "\u00A2",
               "\u00B1",
               "\u2213",
               "\u1D52",
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
               CHAR_UNKNOWN,
            };

            return casioToUnicode[b];
        }

        public string GetAllSources()
        {
            return String.Join($"{NEWLINE}{NEWLINE}", sources.ToArray());
        }

        private bool ReadNext(byte[] source, int position, out byte b1, out byte b2)
        {
            return ReadNext(source, position, out b1, out b2, false, 0);
        }

        private bool ReadNext(byte[] source, int position, out byte b1, out byte b2, bool skip, byte skipByteValue)
        {
            b1 = 0;
            b2 = 0;

            while (skip && position < source.Length && source[position] == skipByteValue)
            {
                position++;
            }

            if (skip && position == source.Length)
            {
                //no bytes found after skip value
                return false;
            }

            b1 = source[position];

            if (position + 1 < source.Length)
            {
                b2 = source[position + 1];

                return true;
            }
            else
            {
                return false;
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
        private const byte B_WRITE = 0x4E;
        private const byte B_PASS = 0x53;
        private const byte B_EDIT = 0x57;
        private const byte B_LLIST = 0x58;
        private const byte B_LOAD = 0x59;
        private const byte B_TRON = 0x5D;
        private const byte B_TROFF = 0x5F;
        private const byte B_VERIFY = 0x60;
        private const byte B_POKE = 0x63;
        private const byte B_CLEAR = 0x6A;
        private const byte B_NEW = 0x6B;
        private const byte B_SAVE = 0x6C;
        private const byte B_ANGLE = 0x6E;
        private const byte B_BEEP = 0x70;
        private const byte B_CLS = 0x71;
        private const byte B_CLOSE = 0x72;
        private const byte B_DEF = 0x76; //undocumented
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
        private const byte B_OUT = 0x99;
        private const byte B_ON = 0x9A;
        private const byte B_CALCJMP = 0x9F;  //undocumented
        private const byte B_PRINT = 0xA3;
        private const byte B_LPRINT = 0xA4;
        private const byte B_PUT = 0xA5;
        private const byte B_READ = 0xA8;
        private const byte B_REM = 0xA9;
        private const byte B_SET = 0xAC;
        private const byte B_STOP = 0xAE;
        private const byte B_MODE = 0xB0;

        private const byte DB_BASIC_GROUP_5 = 0x05;
        private const byte B_ERL = 0x4F;
        private const byte B_ERR = 0x50;
        private const byte B_PI = 0x60;
        private const byte B_CUR = 0x63;
        private const byte B_FACT = 0x67;
        private const byte B_SIN = 0x6B;
        private const byte B_COS = 0x6C;
        private const byte B_TAN = 0x6D;
        private const byte B_ASN = 0x6E;
        private const byte B_ACS = 0x6F;
        private const byte B_ATN = 0x70;
        private const byte B_HYPSIN = 0x71;
        private const byte B_HYPCOS = 0x72;
        private const byte B_HYPTAN = 0x73;
        private const byte B_HYPASN = 0x74;
        private const byte B_HYPACS = 0x75;
        private const byte B_HYPATN = 0x76;
        private const byte B_LN = 0x77;
        private const byte B_LOG = 0x78;
        private const byte B_EXP = 0x79;
        private const byte B_SQR = 0x7A;
        private const byte B_ABS = 0x7B;
        private const byte B_SGN = 0x7C;
        private const byte B_INT = 0x7D;
        private const byte B_FIX = 0x7E;
        private const byte B_FRAC = 0x7F;
        private const byte B_PEEK = 0x86;
        private const byte B_EOF = 0x8A;
        private const byte B_FRE = 0x8D;
        private const byte B_ROUND = 0x90;
        private const byte B_VALF = 0x92;
        private const byte B_RAN = 0x93;
        private const byte B_ASC = 0x94;
        private const byte B_LEN = 0x95;
        private const byte B_VAL = 0x96;
        private const byte B_DEG = 0x9C;
        private const byte B_REC = 0xA7;
        private const byte B_POL = 0xA8;
        private const byte B_NPR = 0xAA;
        private const byte B_NCR = 0xAB;

        private const byte DB_BASIC_GROUP_6 = 0x06;
        private const byte B_DMS = 0x97;
        private const byte B_INPUT = 0x9B;
        private const byte B_MID = 0x9C;
        private const byte B_RIGHT = 0x9D;
        private const byte B_LEFT = 0x9E;
        private const byte B_CHR = 0xA0;
        private const byte B_STR = 0xA1;
        private const byte B_HEX = 0xA3;
        private const byte B_INKEY = 0xA8;
        private const byte B_CALC = 0xAD;

        private const byte DB_BASIC_GROUP_7 = 0x07;
        private const byte B_THEN = 0x47;
        private const byte B_ELSE = 0x48;
        private const byte B_GOTO = 0x49;
        private const byte B_TAB = 0xB6;
        private const byte B_ALL = 0xBB;
        private const byte B_AS = 0xBC;
        private const byte B_STEP = 0xC0;
        private const byte B_TO = 0xC1;
        private const byte B_NOT = 0xC3;
        private const byte B_AND = 0xC4;
        private const byte B_OR = 0xC5;
        private const byte B_XOR = 0xC6;
        private const byte B_MOD = 0xC7;

        private const byte DB_BASIC_JP = 0x03;

        //other control bytes

        private const byte C_LINE_END = 0x00;
        private const byte C_PROGRAM_END = 0x00;
        private const byte C_SPACE = 0x20;
        private const byte C_SEMICOLON = 0x3B;
        private const byte C_EQUALS = 0x3D;
        private const byte C_PAREN_OPEN = 0x28;
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
