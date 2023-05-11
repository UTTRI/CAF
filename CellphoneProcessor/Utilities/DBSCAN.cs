using System.Runtime.CompilerServices;

namespace CellphoneProcessor.Utilities;

internal static class DBSCAN
{
    // TODO: Double check the values
    const double minDistance = 0.1;
    const int minPts = 5;

    public static (double Lon, double Lat, int clusters) GetHouseholdZone(List<Point> points)
    {
        int currentCluster = 1;

        // Create the temporary arrays
        Span<bool> visited = stackalloc bool[points.Count];
        Span<int> cluster = stackalloc int[points.Count];
        Span<int> neighbourStack = stackalloc int[points.Count];
        int currentStackIndex = 0;

        // Initialize our temporary variables
        for (int i = 0; i < visited.Length; i++)
        {
            visited[i] = false;
            cluster[i] = 0;
            neighbourStack[i] = -1;
        }

        // Process each point
        for (int i = 0; i < points.Count; i++)
        {
            if (visited[i])
            {
                continue;
            }
            visited[i] = true;
            // Find the neighbours for this point
            currentStackIndex = 0;
            AddNeighbours(points, i, neighbourStack, ref currentStackIndex);

            if (currentStackIndex < minPts)
            {
                cluster[i] = -1; // mark as noise/outlier
            }
            else
            {
                cluster[i] = currentCluster;
                ExpandCluster(points, neighbourStack, visited, cluster, currentCluster, ref currentStackIndex);
                currentCluster++;
            }
        }

        // Check to see if we failed to have any clusters and just return a failure
        if (currentCluster <= 1)
        {
            return (-1.0, -1.0, 0);
        }

        // Find the largest cluster sufficient cluster and compute the average point
        Span<double> utility = stackalloc double[points.Count];
        Span<int> count = stackalloc int[points.Count];
        Span<double> clusterX = stackalloc double[points.Count];
        Span<double> clusterY = stackalloc double[points.Count];
        for (int i = 0; i < cluster.Length; i++)
        {
            // Go through each point compute the utility of the point, and add it to the cluster
            int clusterIndex = cluster[i] - 1;
            if (clusterIndex < 0) continue;
            // if this point isn't noise then update the cluster
            utility[clusterIndex] += points[i].NightStart ? 2.0 * points[i].Pings : points[i].Pings;
            clusterX[clusterIndex] += points[i].Lon;
            clusterY[clusterIndex] += points[i].Lat;
            count[clusterIndex]++;
        }
        // Now check to see which index has the maximum utility
        int maxUtilityIndex = 0;
        for (int i = 1; i < currentCluster; i++)
        {
            if (utility[i] > utility[maxUtilityIndex])
            {
                maxUtilityIndex = i;
            }
        }
        // Return back the lon / lat of the household
        return (clusterX[maxUtilityIndex] / count[maxUtilityIndex],
            clusterY[maxUtilityIndex] / count[maxUtilityIndex],
            currentCluster - 1);
    }

    private static void AddNeighbours(List<Point> points, int pointIndex, Span<int> neighbours, ref int currentStackIndex)
    {
        Span<int> localStack = stackalloc int[points.Count];
        int localIndex = 0;
        for (int i = 0; i < points.Count; i++)
        {
            if (i == pointIndex) continue;
            if (Distance(points[pointIndex], points[i]) <= minDistance)
            {
                localStack[localIndex++] = i;
            }
        }
        // If it does not meet the minimum number of points don't add it
        if (localIndex >= minPts)
        {
            // if there were enough points make sure to only add ones that are not already there
            var localsAdded = 0;
            for (int i = 0; i < localIndex; i++)
            {
                var add = true;
                for (int j = 0; j < currentStackIndex; j++)
                {
                    if (localStack[i] == neighbours[j])
                    {
                        add = false;
                    }
                }
                if (add)
                {
                    neighbours[currentStackIndex + localsAdded] = localStack[i];
                    localsAdded++;
                }
            }
            currentStackIndex += localsAdded;
        }
    }

    private static void ExpandCluster(List<Point> points, Span<int> neightbours, Span<bool> visited,
        Span<int> cluster, int clusterNumber, ref int neighbourIndex)
    {
        for (int i = 0; i < neighbourIndex; i++)
        {
            if (!visited[neightbours[i]])
            {
                visited[neightbours[i]] = true;
                AddNeighbours(points, i, neightbours, ref neighbourIndex);
            }
            if (cluster[neightbours[i]] == 0)
            {
                cluster[neightbours[i]] = clusterNumber;
            }
        }
    }

    private static double Distance(Point p, Point q)
    {
        return ComputeDistance(p.Lat, p.Lon, q.Lat, q.Lon);
    }

    /// <summary>
    /// Approximates the distance between two lat/lon points.  
    /// Relatively accurate within ~4000KM.
    /// https://en.wikipedia.org/wiki/Haversine_formula
    /// https://rosettacode.org/wiki/Haversine_formula#C++
    /// </summary>
    /// <param name="lat1"></param>
    /// <param name="lon1"></param>
    /// <param name="lat2"></param>
    /// <param name="lon2"></param>
    /// <returns>The distances are in KMs.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static double ComputeDistance(double lat1, double lon1, double lat2, double lon2)
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        static double DegreeToRadian(double deg) => deg * (Math.PI / 180.0);

        const double earthRadius = 6371.0; // Radius of the earth in km
        double latRad1 = DegreeToRadian(lat1);
        double latRad2 = DegreeToRadian(lat2);
        double lonRad1 = DegreeToRadian(lon1);
        double lonRad2 = DegreeToRadian(lon2);

        double diffLa = latRad2 - latRad1;
        double doffLo = lonRad2 - lonRad1;

        double computation = Math.Asin(Math.Sqrt(Math.Sin(diffLa / 2) * Math.Sin(diffLa / 2)
            + Math.Cos(latRad1) * Math.Cos(latRad2) * Math.Sin(doffLo / 2) * Math.Sin(doffLo / 2)));
        return (2 * earthRadius * computation);
    }
}
