using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using H3.Extensions;
using H3.Model;
using Microsoft.SqlServer.Types;

#nullable enable

[assembly: InternalsVisibleTo("H3.Benchmarks")]
namespace H3.Algorithms; 


/// <summary>
/// The vertex testing mode to use when checking containment during
/// polyfill operations.
/// </summary>
public enum VertexTestMode {
    /// <summary>
    /// Specifies that the index's center vertex should be contained
    /// within the geometry.  This matches the polyfill behaviour of
    /// the upstream library.
    /// </summary>
    Center,

    /// <summary>
    /// Specifies that any of the index's boundary vertices can be
    /// contained within the geometry.
    /// </summary>
    Any,

    /// <summary>
    /// Specifies that all of the index's boundary vertices must be
    /// contained within the geometry.
    /// </summary>
    All
}

/// <summary>
/// Polyfill algorithms for H3Index.
/// </summary>
public static class Polyfill {

    /// <summary>
    /// Returns all of the H3 indexes that are contained within the provided
    /// <see cref="Geometry"/> at the specified resolution.  Supports Polygons with holes.
    /// </summary>
    /// <param name="polygon">Containment polygon</param>
    /// <param name="resolution">H3 resolution</param>
    /// <param name="testMode">Specify which <see cref="VertexTestMode"/> to use when checking
    /// index vertex containment.  Defaults to <see cref="VertexTestMode.Center"/></param>.
    /// <returns>Indices that are contained within polygon</returns>
    public static IEnumerable<H3Index> Fill(this SqlGeography polygon, int resolution, VertexTestMode testMode = VertexTestMode.Center) {
        if (polygon.STIsEmpty()) return Enumerable.Empty<H3Index>();

        //is this an issue for SqlGeography? If in doubt, yes! In practise: wait until it becomes a problem.
        // var isTransMeridian = polygon.IsTransMeridian();
        // var testPoly = isTransMeridian ? SplitGeometry(polygon) : polygon;

        Dictionary<ulong, bool> searched = new();
        Stack<H3Index> toSearch = new();
        toSearch.Push( polygon.EnvelopeCenter().ToH3Index(resolution));

        return testMode switch {
            VertexTestMode.All => FillUsingAllVertices(polygon, toSearch, searched),
            VertexTestMode.Any => FillUsingAnyVertex(polygon, toSearch, searched),
            VertexTestMode.Center => FillUsingCenterVertex(polygon, toSearch, searched),
            _ => throw new ArgumentOutOfRangeException(nameof(testMode), "invalid vertex test mode")
        };
    }

    /// <summary>
    /// Performs a polyfill operation utilizing the center <see cref="LatLng"/> of each index produced
    /// during the fill.
    /// </summary>
    /// <param name="polygon"></param>
    /// <param name="toSearch"></param>
    /// <param name="searched"></param>
    /// <returns></returns>
    private static IEnumerable<H3Index> FillUsingCenterVertex(SqlGeography polygon, Stack<H3Index> toSearch, IDictionary<ulong, bool> searched) {

        while (toSearch.Count != 0) {
            var index = toSearch.Pop();

            foreach (var neighbour in index.GetNeighbours()) {
                if (searched.ContainsKey(neighbour.Value)) continue;
                searched[neighbour.Value] = true;

                if (!polygon.STIntersects(neighbour.ToCoordinate()))
                {
                    continue;
                }

                yield return neighbour;
                toSearch.Push(neighbour);
            }
        }
    }

    /// <summary>
    /// Performs a polyfill operation utilizing any <see cref="LatLng"/> from the cell boundary of each
    /// index produced during the fill.
    /// </summary>
    private static IEnumerable<H3Index> FillUsingAnyVertex(SqlGeography polygon, Stack<H3Index> toSearch, IDictionary<ulong, bool> searched) {


        while (toSearch.Count != 0) {
            var index = toSearch.Pop();

            foreach (var neighbour in index.GetNeighbours()) {
                if (searched.ContainsKey(neighbour.Value)) continue;
                searched[neighbour.Value] = true;

                if(!polygon.STIntersects(neighbour.GetCellBoundary()))
                {
                    continue;
                }

                yield return neighbour;
                toSearch.Push(neighbour);
            }
        }
    }

    /// <summary>
    /// Performs a polyfill operation utilizing all <see cref="LatLng"/>s from the cell boundary of each
    /// index produced during the fill.
    /// </summary>
    private static IEnumerable<H3Index> FillUsingAllVertices(SqlGeography polygon, Stack<H3Index> toSearch, IDictionary<ulong, bool> searched) {


        while (toSearch.Count != 0) {
            var index = toSearch.Pop();

            foreach (var neighbour in index.GetNeighbours()) {
                if (searched.ContainsKey(neighbour.Value)) continue;
                searched[neighbour.Value] = true;

                if(!polygon.STContains(neighbour.GetCellBoundary()))
                {
                    continue;
                }

                yield return neighbour;
                toSearch.Push(neighbour);
            }
        }
    }

    //TBB: NOPE'ing out of this implementation cos am too thick
    /*
    /// <summary>
    /// Returns all of the H3 indexes that follow the provided LineString
    /// at the specified resolution.
    /// </summary>
    /// <param name="polyLine"></param>
    /// <param name="resolution"></param>
    /// <returns></returns>
    public static IEnumerable<H3Index> Fill(this SqlGeography polyLine, int resolution)
    {
        if (polyLine.STGeometryType() == "LineString")
        {
            polyLine.TraceCoordinates(resolution);
        }

        return new List<H3Index> { H3Index.Invalid };
    }


    /// <summary>
    /// Gets all of the H3 indices that define the provided set of <see cref="geography"/>s.
    /// </summary>
    /// <param name="geography"></param>
    /// <param name="resolution"></param>
    /// <returns></returns>
    public static IEnumerable<H3Index> TraceCoordinates(this SqlGeography geography, int resolution) {
        HashSet<H3Index> indices = new();

        // trace the coordinates
        var coordLen = geography.STNumPoints().Value - 1;
        FaceIJK faceIjk = new();
        LatLng v1 = new();
        LatLng v2 = new();
        Vec3d v3d = new();
        for (var c = 0; c < coordLen; c += 1) {
            // from this coordinate to next/first
            var vA = geography.STPointN(c + 1);
            var vB = geography.STPointN(c + 2);
            v1.Longitude = vA.Long.Value * M_PI_180;
            v1.Latitude = vA.Lat.Value * M_PI_180;
            v2.Longitude = vB.Long.Value * M_PI_180;
            v2.Latitude = vB.Lat.Value * M_PI_180;

            // estimate number of indices between points, use that as a
            // number of segments to chop the line into
            var count = v1.LineHexEstimate(v2, resolution);

            for (var j = 1; j < count; j += 1) {
                // interpolate line
                var interpolated = LinearLocation.PointAlongSegmentByFraction(vA, vB, (double)j / count);
                indices.Add(interpolated.ToH3Index(resolution, faceIjk, v3d));
            }
        }

        return indices;
    }*/

}