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

### AddRoadTimestoSurvey

This project is designed to append road times and distances to survey records in a new CSV file where each record will directly
correspond to a record from the incoming survey data.  The survey columns are ignored except for columns 5,6 and 8,9 where
they represent the origin's lat/long and destination's lat/long respectively.

This program is designed to be used on with a command line / terminal where it takes in three parameters in the given order:

1. Network File Path - The location of the .osmx file to read in that represents the network.  If a cache file has already been generated, it will load that in instead of the .osmx automatically.
1. Survey File Path - The location of the survey CSV file to get the trip coordinates from.
1. Output File Path - The location to save the CSV records for road time and distance mirroring each row in the survey file.

#### Building

To build this project you will need to have the .Net 7 SDK installed.  This program should be able to be compiled and run
on any operating system that supports .Net 7, not just Windows.

> dotnet build -c Release

This will generate an optimized build of the software.


#### Running

To run the software after compiling it, you can execute the command:

> dotnet run -c Release [NetworkFilePath.osmx] [SurveyTrips.csv] [RoadLoSResults.csv]

Or

> AddRoadtimesToSurvey.exe [NetworkFilePath.osmx] [SurveyTrips.csv] [RoadLoSResults.csv]

### Create Demand Matrices

This project is designed to take in trip records and generate Road Distances, Road Times, and cluster gap times broken down
by weekend and weekday time periods.  The program takes in three parameters:

1. RecordsPath - The location of the TripRecords to load in.
1. OutputPath - The location of the directory to store all of the CSV files to.
1. HourlyOffset - The GMT offset to apply when computing weather a record is done during the weekend or weekday.  e.g. Bogota and Panama are -5, and Buenbos Aires is -3.

#### Building

To build this project you will need to have the .Net 7 SDK installed.  This program should be able to be compiled and run
on any operating system that supports .Net 7, not just Windows.

> dotnet build -c Release

#### Running

To run the software after compiling it, you can execute the command:

> dotnet run -c Release [RecordsPath] [OutputPath] [HourlyOffset]

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

Alternatively you can run the `CellphoneProcessor.exe` generated when building the software in
`~/CellphoneProcessor/bin/Release/net7.0-windows/CellphoneProcessor.exe`.

#### Setting up Open Street Maps

In order to generate the level of service variables for mode choice imputation we will need to setup an OTP server
to connect to that will generate the paths through the network.  For our implementation we used OPT version 1.4. However
when we found that the code is also compatible with OTP 2.2 though the results that it generated were different from the
1.4 implementation.
