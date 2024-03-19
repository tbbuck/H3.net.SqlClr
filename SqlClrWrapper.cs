using System;
using System.Collections;
using System.Data.SqlTypes;
using H3.Algorithms;
using H3.Extensions;
using Microsoft.SqlServer.Server;
using Microsoft.SqlServer.Types;

namespace H3;

public class SqlClrWrapper
{
        [SqlFunction( DataAccess = DataAccessKind.None, SystemDataAccess = SystemDataAccessKind.None, IsDeterministic = true)]
        public static SqlInt64 PointToH3Index(SqlGeography point, SqlInt32 resolution)
        {
            return point.ToH3Index(resolution.Value).ValueSqlInt64;
        }

        [SqlFunction( DataAccess = DataAccessKind.None, SystemDataAccess = SystemDataAccessKind.None, IsDeterministic = true)]
        public static SqlInt64 LatLongToH3Index(SqlDouble latitude, SqlDouble longitude, SqlInt32 resolution)
        {
            return H3Index.FromPoint(SqlGeography.Point(latitude.Value, longitude.Value, 4326), resolution.Value).ValueSqlInt64;
        }

        [SqlFunction( DataAccess = DataAccessKind.None, SystemDataAccess = SystemDataAccessKind.None, IsDeterministic = true)]
        public static SqlGeography H3IndexToBoundary(SqlInt64 index)
        {
            return new H3Index(index).GetCellBoundary();
        }

        [SqlFunction( DataAccess = DataAccessKind.None, SystemDataAccess = SystemDataAccessKind.None, IsDeterministic = true)]
        public static SqlGeography H3IndexToPoint(SqlInt64 index)
        {
            return new H3Index(index).ToPoint();
        }

        [SqlFunction( DataAccess = DataAccessKind.None, SystemDataAccess = SystemDataAccessKind.None, IsDeterministic = true)]
        public static Int32 H3GridDistance(SqlInt64 origin, SqlInt64 destination)
        {
            return new H3Index(origin).GridDistance(new H3Index(destination));
        }

        [SqlFunction(DataAccess = DataAccessKind.None, SystemDataAccess = SystemDataAccessKind.None, IsDeterministic = true, FillRowMethodName = "FillH3IndexList")]
        public static IEnumerable PolyFill(SqlGeography polygon, int resolution)
        {
            return polygon.Fill(resolution);
        }

        [SqlFunction(DataAccess = DataAccessKind.None, SystemDataAccess = SystemDataAccessKind.None, IsDeterministic = true, FillRowMethodName = "FillH3IndexList")]
        public static IEnumerable PolyFillOverlapping(SqlGeography polygon, int resolution)
        {
            return polygon.Fill(resolution, VertexTestMode.Any);
        }

        [SqlFunction(DataAccess = DataAccessKind.None, SystemDataAccess = SystemDataAccessKind.None, IsDeterministic = true, FillRowMethodName = "FillH3IndexList")]
        public static IEnumerable PolyFillInside(SqlGeography polygon, int resolution)
        {
            return polygon.Fill(resolution, VertexTestMode.All);
        }

        [SqlFunction(DataAccess = DataAccessKind.None, SystemDataAccess = SystemDataAccessKind.None, IsDeterministic = true)]
        public static SqlGeography PolyFillToMultiPolygon(SqlGeography polygon, int resolution)
        {
            return polygon.Fill(resolution).GetCellBoundaries();
        }

        [SqlFunction(DataAccess = DataAccessKind.None, SystemDataAccess = SystemDataAccessKind.None, IsDeterministic = true, FillRowMethodName = "FillH3IndexList")]
        public static IEnumerable PolyFillCompact(SqlGeography polygon, int resolution)
        {
            return polygon.Fill(resolution).CompactCells();
        }

        public static void FillH3IndexList(Object obj, out SqlInt64 iIdx64)
        {
            iIdx64 = ((H3Index)obj).ValueSqlInt64;
        }

        [SqlFunction( DataAccess = DataAccessKind.None, SystemDataAccess = SystemDataAccessKind.None, IsDeterministic = true, FillRowMethodName = "FillH3IndexRange")]
        public static IEnumerable H3IndexRange(SqlInt64 i64Idx, SqlInt32 k)
        {
            return new H3Index(i64Idx).GridDiskDistances(k.Value);
        }

        public static void FillH3IndexRange(Object obj, out SqlInt64 iIdx64)
        {
            iIdx64 = ((RingCell)obj).Index.ValueSqlInt64;
        }

        [SqlFunction(DataAccess = DataAccessKind.None, SystemDataAccess = SystemDataAccessKind.None, IsDeterministic = true, FillRowMethodName = "FillH3IndexRangeDistance")]
        public static IEnumerable H3IndexRangeDistance(SqlInt64 i64Idx, SqlInt32 k)
        {
            return new H3Index(i64Idx).GridDiskDistances(k.Value);
        }

        public static void FillH3IndexRangeDistance(Object obj, out SqlInt64 iIdx64, out Int32 iDistance)
        {
            var ringCell = (RingCell)obj;

            iIdx64 = ringCell.Index.ValueSqlInt64;
            iDistance = ringCell.Distance;
        }
}