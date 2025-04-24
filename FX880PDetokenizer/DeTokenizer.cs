using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FX880PDetokenizer
{
    class DeTokenizer
    {
        StringBuilder _sbOutput;

        private byte[] _source;

        uint _position = 0;

        #region constants

        private const string NEWLINE = "\r\n";
        private const char SPACER = ' ';

        #endregion

        public DeTokenizer(byte[] source, uint position)
        {
            _source = source;

            if( _position < source.Length )
            {
                _position = position;
            }
        }

        public void DeTokenize()
        {
            bool openProgram = false;

            int lineNumber;

            int lineBytesRemaining;

            uint openPosition = 0;

            byte b1,b2,b3; //single bytes

            _sbOutput = new StringBuilder();

            while (_position < _source.Length)
            {
                ReadNext(out b1, out b2);

                if (_position + 3 >= _source.Length || _position + b1 >= _source.Length)
                {
                    _position++;

                    break;
                }

                //start of a line is the "count" of bytes for that line. look forward by this count and if we have zero, thats the end of the line
                //this is likely the start of a valid BASIC line
                //also check that the byte right after the line number bytes is 0x20
                if (_source[_position + b1] == C_LINE_END && _source[_position + 3] == 0x20)
                {
                    lineBytesRemaining = b1 + 1;

                    openPosition = _position;

                    //line number is next two bytes, followed by a space (0x20)
                    lineNumber = ReadTwo(_position + 1);

                    _sbOutput.Append($"{lineNumber} ");

                    _position += 4;

                    lineBytesRemaining -= 4;

                    while (lineBytesRemaining > 0)
                    {
                        ReadNext(out b1, out b2);

                        if (b1 == DB_BASIC_1)
                        {
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

                                default:

                                    //unknown command

                                    break;
                            }

                            _position += 2;

                            lineBytesRemaining -= 2;
                        }
                        else if (b1 == DB_BASIC_2)
                        {
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

                                default:

                                    break;
                            }

                            _position += 2;

                            lineBytesRemaining -= 2;
                        }
                        else if (b1 == DB_BASIC_3)
                        {
                            //command
                            switch (b2)
                            {
                                case B_PEEK:

                                    _sbOutput.Append($"PEEK");

                                    break;

                                default:

                                    break;
                            }

                            _position += 2;

                            lineBytesRemaining -= 2;
                        }
                        else if (b1 == DB_BASIC_4)
                        {
                            //command
                            switch (b2)
                            {
                                case B_CHR:

                                    _sbOutput.Append($"CHR$");

                                    break;

                                default:

                                    break;
                            }

                            _position += 2;

                            lineBytesRemaining -= 2;
                        }
                        else if (b1 == DB_BASIC_5)
                        {
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
                        }
                        else if (b1 == DB_BASIC_JP)
                        {
                            //line number
                            _sbOutput.Append($"{ReadTwo(_position + 1)}");

                            _position += 3;

                            lineBytesRemaining -= 3;
                        }
                        else
                        {
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
                    _position++;
                }
            }
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

        private int ReadTwo(uint position)
        {
            int val = 0;
            
            if (position + 1 < _source.Length)
            {
                val = _source[position + 1] * 256 + _source[position];
            }

            return val;
        }

        public string GetOutput()
        {
            return _sbOutput.ToString();
        }

        private const byte DB_BASIC_1 = 0x04;
        private const byte B_REM = 0xA9;
        private const byte B_GOSUB = 0x4A;
        private const byte B_RETURN = 0x4B;
        private const byte B_FOR = 0x81;
        private const byte B_NEXT = 0x82;
        private const byte B_IF = 0x8D;
        private const byte B_EDIT = 0x57;
        private const byte B_CLEAR = 0x6A;
        private const byte B_OPEN = 0x97;
        private const byte B_DEFSEG = 0x78;
        private const byte B_PRINT = 0xA3;
        private const byte B_GOTO = 0x49;
        private const byte B_CLOSE = 0x72;

        private const byte DB_BASIC_2 = 0x07;
        private const byte B_AS = 0xBC;
        private const byte B_THEN = 0x47;

        private const byte DB_BASIC_3 = 0x05;
        private const byte B_PEEK = 0x86;

        private const byte DB_BASIC_4 = 0x06;
        private const byte B_CHR = 0xA0;

        private const byte DB_BASIC_5 = 0x02;
        private const byte B_REM2 = 0x45;

        private const byte DB_BASIC_JP = 0x03;

        private const byte C_LINE_END = 0x00;
        private const byte C_SPACE = 0x20;
        private const byte C_MULTI_STATEMENT_SEPARATOR = 0x01;
    }
}
