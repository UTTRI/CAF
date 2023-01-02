using RoadNetwork;

// Make sure that we have the right number of arguments
if(args.Length <= 5)
{
    Console.WriteLine("USAGE: BaseNetwork.osmx ZoneSystem.csv Demand.csv OutputNetwork.osmx.cache TravelTimes.csv");
    return;
}

var baseNetworkFile = args[0];
var zoneSystemFile = args[1];
var demandMatrixFile = args[2];
var outputNetworkFile = args[4];
var finalTravelTimesFile = args[5];

var baseNetwork = new Network(baseNetworkFile);
var zoneSystem = new ZoneSystem(zoneSystemFile);
var demandMatrix = Matrix.LoadMatrixFromCSV(demandMatrixFile, zoneSystem);

RoadAssignment.ApplyDemandToNetwork(baseNetwork, zoneSystem, demandMatrix);

var matrix = RoadAssignment.GetTravelTimes(baseNetwork, zoneSystem);

matrix.Save(zoneSystem, finalTravelTimesFile);

baseNetwork.SaveNetwork(outputNetworkFile);