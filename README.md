# Casio FX-880P DeTokenizer

![Image](https://github.com/user-attachments/assets/640a7728-1324-42bf-9c14-ec601b730d42)

This program is designed to decompile or "detokenize" BASIC routines stored within a Casio FX-880P.  The input to the program is a binary image of memory dumped from the FX-880P (using a serial interface such as an FA-6 or homebrew) and the output of the program is ASCII BASIC (the source).  This project is a work in progress and will be updated as I find gaps or bugs.

I searched the interwebs for a program that will do this, as I wanted to look at the BASIC source for the 116 built-in scientific library functions in the ROM of the pocket computer.  I was unable to find anything specific for the Casio FX-880P.  The closest application I found was Marcus von Cube's CASsette IO Utilities (https://www.mvcsys.de/doc/casioutil.html) and LIST850 looked promising.  However, I was unable to output any source and ran into an error.

With no documentation I could find on the internal tokenization format for the Casio FX-880P, I reverse engineered the format as best I could.

Usage: FX880PDeTokenizer.exe -i segment0.bin [-a offset] -o basic.txt

The optional switch -a offset can be specified as a hexadecimal number without any prefixes. If omitted, a default value 0000 is assumed and the detokenization will start from the beginning of the input image. 

Example: FX880PDeTokenizer.exe -i segment0.bin -a 0x4000 -o basic.txt

In Visual Studio you can set the Debug Options in the Project Properties to:

-i ..\..\resources\segment0.bin -o ..\..\resources\basic.txt

This will untokenize the included sample image and output the result in the resources folder starting at an offset of 0.

Note that this program by default will try to find the program areas P0-P9 and output each to a file, if a program is present for each one.  In the example above, a file per program area will be output: basic0.txt through basic9.txt (assuming all 10 program areas had source within them).  The program is assuming that the image is the user RAM area.  The sample provided in the project (segment0.bin) is from my unit with a single program loaded into P0.

This program may also work on other versions of this model, such as the FX-850P.

I can be contacted at jbertier@arrl.net
