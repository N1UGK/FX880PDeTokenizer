# FX880PDeTokenizer

This program is designed to decompile or "untokenize" BASIC stored within a Casio FX-880P.  The input to the program is a binary image of memory dumped from the FX-880P (using a serial interface such as an FA-6 or homebrew) and the output of the program is ASCII BASIC (the source).  This project is a work in progress and will be updated as I find gaps or bugs.

I searched the interwebs for a program that will do this, as I wanted to look at the BASIC source for the 116 built-in scientific library functions in the ROM of the pocket computer.  I was unable to find anything specific for the Casio FX-880P.  The closest application I found was Marcus von Cube's CASsette IO Utilities (https://www.mvcsys.de/doc/casioutil.html) and LIST850 looked promising.  However, I was unable to output any source and ran into an error.

With no documentation I could find on the internal tokenization format for the Casio FX-880P, I reverse engineered the format as best I could.

Usage: FX880PDeTokenizer.exe -i infile.bin [-a offset] -o basic.txt

The optional switch -a offset can be specified as a hexadecimal number without any prefixes. If omitted, a default value 0000 is assumed and the detokenization will start from the beginning of the input image. 

Example: FX880PDeTokenizer.exe -i infile.bin -a 0x4000 -o basic.txt

In Visual Studio you can set the Debug Options in the Project Properties to:

-i ..\..\resources\infile.bin -o ..\..\resources\basic.txt

This will untokenize the included sample image and output the result in the resources folder starting at an offset of 0.

Note that this program currently does not yet read the start and end address vectors for program areas P0-P9. It scours the entire file (starting at the optional offset) and outputs all valid BASIC lines it finds.  This means it could output previously deleted lines, deleted programs, partial programs in addition to the current valid programs.

I can be contacted at jbertier@arrl.net
