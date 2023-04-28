# Software for CAF 

Contained in this repository is a set of software used for _iCITY-SOUTH: Urban Informatics for Sustainable 
Metropolitan Growth in Latin America_.

## Software

Contained within this repository are multiple programs and Jupyter Notebooks designed to process and
analyze the cellphone trace data with the goal of generating trips with modes in addition to some console
tooling for generating some additional reporting.  Each section will explain how to compile and use the
software.

To compile the software you will need to have the .Net 7 SDK installed on your system.  You can download .Net from
[here](https://dotnet.microsoft.com/en-us/download/dotnet/7.0).  While the command-line tools can be compiled and run
from Linux or Mac, CellphoneProcessor will require you to be building it and running it from Windows 10+.  If you already
have Visual Studio version 17.4+ installed you have .Net 7 installed alongside it.

### CellphoneProcessor

This software is designed to automate the downloading and initial chunking of the cellphone records,
convert the raw cellphone records into stays, convert the stays into trips, and finally attach
transit LoS information to those trip records.  These records can then be input into the mode choice
notebooks to get out demand.

#### Building

To build this project you will need to be on a machine running Windows 10+ where with the .Net 7 SDK installed.
In your terminal go into the CellphoneProcessor directory and then run the following command.

> dotnet build -c Release

This will generate an optimized build of the software.

#### Running

To run the software after compiling it, you can execute the command:

> dotnet run -c Release
