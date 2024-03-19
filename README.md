<img align="right" src="https://uber.github.io/img/h3Logo-color.svg" alt="H3 Logo" width="200">

# H3.net: A port of [H3.net](https://github.com/pocketken/H3.net) to SQL Server via C# and SQLClr
This is a port of [H3.net](https://github.com/pocketken/H3.net) to SQL Server via SQL CLR, with most of the polygon functionality based on [SqlGeography](https://learn.microsoft.com/en-us/dotnet/api/microsoft.sqlserver.types.sqlgeography?view=sql-dacfx-161) for native SQL Server Geography use.  It supports `net481` and uses the latest release of H3.net.

H3 is a geospatial indexing system using a hexagonal grid that can be (approximately) subdivided into finer and finer hexagonal grids, combining the benefits of a hexagonal grid with [S2](https://code.google.com/archive/p/s2-geometry-library/)'s hierarchical subdivisions.

## Installing
You will need to build a DLL for SQL Server to import as an assembly, and then run SQL commands to install the functions that you can use.

While I have developed this project on macOS Sonoma and it _should_ build on Windows and Linux, please tell me if you run into problems.

### Steps
#### 1. Enable CLR support in SQL Server if you haven't already

```tsql
exec sp_configure 'clr enabled', 1
reconfigure
GO
```

#### 2. Build the project

```shell
dotnet build -c Release
```

#### 3. Copy the build files somewhere MSSQL can access them

```shell
cp bin/Release/net481/H3.net.SqlClr.dll bin/Release/net481/H3.net.SqlClr.pdb bin/Release/net481/Microsoft.SqlServer.Server.dll bin/Release/net481/Microsoft.SqlServer.Types.dll /some-directory/mssql-can-access/
```

#### 4. Create the assembly reference

```tsql
use database_name_to_use;

CREATE ASSEMBLY H3NetClr FROM '/path/from/step-3/H3.net.SqlClr.dll' WITH PERMISSION_SET = SAFE; GO
```

#### 5. Install SQL commands

```tsql
CREATE FUNCTION dbo.LatLongToH3Index(@latitude FLOAT, @longitude FLOAT, @resolution INT) RETURNS BIGINT AS EXTERNAL NAME H3NetClr.[H3.SqlClrWrapper].LatLongToH3Index; GO
CREATE FUNCTION dbo.PointToH3Index(@point GEOGRAPHY, @resolution INT) RETURNS BIGINT AS EXTERNAL NAME H3NetClr.[H3.SqlClrWrapper].PointToH3Index; GO
CREATE FUNCTION dbo.H3IndexToPoint(@index BIGINT) RETURNS GEOGRAPHY AS EXTERNAL NAME H3NetClr.[H3.SqlClrWrapper].H3IndexToPoint; GO
CREATE FUNCTION dbo.H3IndexToBoundary(@index BIGINT) RETURNS GEOGRAPHY AS EXTERNAL NAME H3NetClr.[H3.SqlClrWrapper].H3IndexToBoundary; GO
CREATE FUNCTION dbo.H3PolyFill(@polygon GEOGRAPHY, @res INT) RETURNS TABLE(cell_id BIGINT) AS EXTERNAL NAME H3NetClr.[H3.SqlClrWrapper].PolyFill; GO
CREATE FUNCTION dbo.H3PolyFillOverlapping(@polygon GEOGRAPHY, @res INT) RETURNS TABLE(cell_id BIGINT) AS EXTERNAL NAME H3NetClr.[H3.SqlClrWrapper].PolyFillOverlapping; GO
CREATE FUNCTION dbo.H3PolyFillInside(@polygon GEOGRAPHY, @res INT) RETURNS TABLE(cell_id BIGINT) AS EXTERNAL NAME H3NetClr.[H3.SqlClrWrapper].PolyFillInside; GO
CREATE FUNCTION dbo.H3PolyFillCompact(@polygon GEOGRAPHY, @res INT) RETURNS TABLE(cell_id BIGINT) AS EXTERNAL NAME H3NetClr.[H3.SqlClrWrapper].PolyFillCompact; GO
CREATE FUNCTION dbo.H3PolyFillToMultiPolygon(@polygon GEOGRAPHY, @res INT) RETURNS GEOGRAPHY AS EXTERNAL NAME H3NetClr.[H3.SqlClrWrapper].PolyFillToMultiPolygon; GO
CREATE FUNCTION dbo.H3GridDistance(@start BIGINT, @end BIGINT) RETURNS INT AS EXTERNAL NAME H3NetClr.[H3.SqlClrWrapper].H3GridDistance; GO
CREATE FUNCTION dbo.H3IndexRange(@index BIGINT, @k INT) RETURNS TABLE(cell_id BIGINT) AS EXTERNAL NAME H3NetClr.[H3.SqlClrWrapper].H3IndexRange; GO
CREATE FUNCTION dbo.H3IndexRangeDistance(@index BIGINT, @k INT) RETURNS TABLE(cell_id BIGINT, distance INT) AS EXTERNAL NAME H3NetClr.[H3.SqlClrWrapper].H3IndexRangeDistance; GO
```

## Examples

### H3 Index for latitude / longitude pair at resolution 10
```tsql
SELECT dbo.LatLongToH3Index(51.50136599999999, -0.14189, 10);
-- returns BIGINT 621942988316803071 
```

### H3 Index for Geography Point at resolution 9
```tsql
DECLARE @point GEOGRAPHY = GEOGRAPHY::Point(51.50136599999999, -0.14189, 4326);
SELECT dbo.PointToH3Index(@point, 9);
-- returns BIGINT 617439388689498111
```
### H3 Index's central coordinates as Geography Point 
```tsql
SELECT dbo.H3IndexToPoint(617439388689498111);
-- returns Geography type, with STAsText() -> POINT (-0.14287445856532638 51.501744706664091)
```
### H3 Index's Boundary as Geography Polygon
```tsql
SELECT dbo.H3IndexToBoundary(617439388689498111);
-- returns Geography type, with STAsText() ->
-- POLYGON ((-0.1433086747759113 51.503491954526822, -0.14539999922415278 51.502511203725028, -0.1449657187320165 51.500763934493264, -0.14244027052338126 51.499997436569508, -0.14034904040487972 51.500978166766039, -0.1407831641656424 51.502725415490147, -0.1433086747759113 51.503491954526822))
```
### Polyfill a Geography Instance with H3 Indices
#### Using [H3.net's vertex modes](https://github.com/pocketken/H3.net/blob/main/docs/api-regions.md) 

```tsql
DECLARE @polygon GEOGRAPHY = GEOGRAPHY::STGeomFromText('POLYGON ((-122.40898669969356 37.81331899988944, -122.47987669969707 37.81515719990604, -122.52471869969825 37.783587199903444, -122.51234369969448 37.70761319990403, -122.35447369969584 37.719806199904276, -122.38054369969613 37.78663019990699, -122.40898669969356 37.81331899988944))', 4326);

-- resolution 7, VertexTestMode.Center
SELECT cell_id FROM dbo.H3PolyFill(@polygon, 7);

-- resolution 7, VertexTestMode.Any
SELECT cell_id FROM dbo.H3PolyFillOverlapping(@polygon, 7);

-- resolution 7, VertexTestMode.All
SELECT cell_id FROM dbo.H3PolyFillInside(@polygon, 7);

```

### Compact Polyfill, Using Minimum Number of H3 Indices at Various Resolutions

```tsql
DECLARE @polygon GEOGRAPHY = GEOGRAPHY::STGeomFromText('POLYGON ((-122.40898669969356 37.81331899988944, -122.47987669969707 37.81515719990604, -122.52471869969825 37.783587199903444, -122.51234369969448 37.70761319990403, -122.35447369969584 37.719806199904276, -122.38054369969613 37.78663019990699, -122.40898669969356 37.81331899988944))', 4326);
SELECT cell_id FROM dbo.H3PolyFillCompact(@polygon, 9); -- smallest hexagon resolution 9 
```

### Compact Polyfill and Return Scalar Geography MultiPolygon
```tsql
DECLARE @polygon GEOGRAPHY = GEOGRAPHY::STGeomFromText('POLYGON ((-122.40898669969356 37.81331899988944, -122.47987669969707 37.81515719990604, -122.52471869969825 37.783587199903444, -122.51234369969448 37.70761319990403, -122.35447369969584 37.719806199904276, -122.38054369969613 37.78663019990699, -122.40898669969356 37.81331899988944))', 4326);
SELECT dbo.H3PolyFillToMultiPolygon(@polygon, 7); -- nb! returns scalar value, NOT table-valued
--- STAsText() ->
--- MULTIPOLYGON (((-122.4332650748479 37.749955385153285, -122.42576908738397 37.76173573392154, -122.43458610784572 37.772267349851525, -122.45089906210502 37.77101747326509, -122.45839165007875 37.759237120299929, -122.44957468417971 37.74870664801545, -122.4332650748479 37.749955385153285)), ((-122.45839165007875 37.759237120299929, -122.45089906210502 37.77101747326509, [...] , -122.38563455403627 37.776004200673846)))
```

### Grid K-ring Distance Between H3 Indices
```tsql
SELECT dbo.H3GridDistance(608430891680661503,608430896109846527); -- returns 6
```

### Get K-ring H3 Indices Surrounding the H3 Index 
```tsql
SELECT cell_id FROM dbo.H3IndexRange(608430891680661503, 6);  -- k-ring 6
```

### Same, and Include K-ring Distances
```tsql
SELECT cell_id, distance FROM dbo.H3IndexRangeDistance(608430891680661503, 6); -- k-ring 6
```

## Upgrading

After copying across your DLLs, you can refresh SQL Server's copy by running

```tsql
ALTER ASSEMBLY H3NetClr FROM '/path/from/step-3/H3.net.SqlClr.dll'; GO
```

## Uninstalling

### 1. Drop installed SQL functions

```tsql
DROP FUNCTION IF EXISTS dbo.LatLongToH3Index; GO
DROP FUNCTION IF EXISTS dbo.PointToH3Index; GO
DROP FUNCTION IF EXISTS dbo.H3IndexToPoint; GO
DROP FUNCTION IF EXISTS dbo.H3IndexToBoundary; GO
DROP FUNCTION IF EXISTS dbo.H3PolyFill; GO
DROP FUNCTION IF EXISTS dbo.H3PolyFillOverlapping; GO
DROP FUNCTION IF EXISTS dbo.H3PolyFillInside; GO
DROP FUNCTION IF EXISTS dbo.H3PolyFillCompact; GO
DROP FUNCTION IF EXISTS dbo.H3PolyFillToMultiPolygon; GO
DROP FUNCTION IF EXISTS dbo.H3GridDistance; GO
DROP FUNCTION IF EXISTS dbo.H3IndexRange; GO
DROP FUNCTION IF EXISTS dbo.H3IndexRangeDistance; GO
```

### 2. Drop the installed assembly

```tsql
DROP ASSEMBLY IF EXISTS H3NetClr; GO
```