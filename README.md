# RQL.NET
![build status](https://ci.appveyor.com/api/projects/status/github/ashtonian/RQL.NET?branch=master&svg=true) [![time tracker](https://wakatime.com/badge/github/Ashtonian/RQL.NET.svg)](https://wakatime.com/badge/github/Ashtonian/RQL.NET) (since dec'19)

`RQL.NET` is a resource query language for .NET intended for use with web apps. It provides a simple, hackable api for creating dynamic sql queries from user submitted JSON. It is intended to sit between a web application and a SQL based database. It converts user submitted JSON query structures (inspired by mongodb query syntax) to sql queries, handling validation and type conversions. It was inspired by and is mostly compatible via the JSON interface with [rql (golang)](https://github.com/a8m/rql) and mongodb's query language.

<p align="center">
  <img src="assets/diagram.png" alt="rql.net diagram">
</p>

## Why

When creating a simple CRUD api its often a requirement to provide basic collection filtering functionality. Without implementing some other heavy layer (graphql, odata, ef), usually I would end up having to write code to support each field for a given class. A common solution is adding a query parameter for each field, and when using aggregate functions having a separate composite parameter for that aggregate ie `?updated_at_gt=x&updated_at_lt=y`. Outside of that being cumbersome for lots of fields, this begins to totally breakdown when needing to apply a disjunction between two conditions using aggregate functions, something like `SELECT * FROM TABLE WHERE is_done = 1 OR (updated_at < X AND updated_at > Y)`.

## Getting Started

### Basic

Client side json input:

```javascript
var rqlExpression = {
  filter: {
    "isDone": 1,
    "$or":[
        {"updatedAt": {"$lt": "2020/01/02", "$gt":1577836800}},
      ],
  },
  limit : 1000,
  offset: 0,
  sort:["-updatedAt"],
}
```

C#:

```c#
var (dbExpression, errs) = RqlParser.Parse<TestClass>(rqlExpression);
if(errs != null) { /*handle me*/ }

Assert.True(dbExpression.Filter = "IsDone = @isDone AND ( UpdatedAt < @updatedAt AND UpdatedAt > @updatedAt2 )");
Assert.True(dbExpression.Limit == 1000);
Assert.True(dbExpression.Offset == 0);
Assert.True(dbExpression.Sort == "UpdatedAt DESC");
Assert.Equal(result.Parameters["@isDone"], true);
Assert.Equal(result.Parameters["@updatedAt"], new DateTime(2020, 01, 02));
Assert.Equal(result.Parameters["@updatedAt2"], new DateTime(2020, 01, 01));
```

Alternatives:

```c#
// Alternatively you can use a generic instance
IRqlParser<TestClass> genericParser = new RqlParser<TestClass>();
(dbExpression, err) = genericParser.Parse(rqlExpression);

// Alternatively you can use a non-generic instance
var classSpec = new ClassSpecBuilder().Build(typeof(TestClass));
IRqlParser parser = new RqlParser(classSpec);
(dbExpression, err) = parser.Parse(rqlExpression);

// Alternatively parse a `RqlExpression` object
var rqlExpression = new RqlExpression
{
  Filter = new Dictionary<string, object>()
  {
    ["isDone"] = 1,
    ["$or"] = new List<object>()
    {
      new Dictionary<string, object>(){
        ["updatedAt"] = new Dictionary<string,object>(){
            ["$lt"] = "2020/01/02",
            ["$gt"] = 1577836800
        }
      }
    },
  },
  Limit = 1000,
  Offset = 0,
  Sort = new List<string>() { "-updatedAt" },
};
(dbExpression, err) = RqlParser.Parse<TestClass>(rqlExpression);
```

### Integration Examples

#### Web Api

```c#
[HttpPost("api/rql")]
public async void Rql([FromBody] dynamic rqlIn)
{
  var (dbExpression, _) = parser.Parse((rqlIn as object).ToString());
}
```

#### ADO Command

```c#
 using (var connection = await _connectionFactory.GetConnection())
using (SqlCommand command = new SqlCommand())
{
  command.CommandText = $"SELECT * FROM TestClass WHERE ${dbExpression.Filter} LIMIT ${dbExpression.Limit} OFFSET ${dbExpression.Offset} ORDER BY ${dbExpression.Sort}";
  command.Parameters.AddRange(dbExpression.Parameters.Select(x => new SqlParameter(x.Key, x.Value)).ToArray());
  using (var reader = command.ExecuteReader())
  {
      // do stuff
  }
}
```

#### Dapper

```c#
using (var connection = await _connectionFactory.GetConnection())
{
  connection.Open();
  var parameters = new DynamicParameters(dbExpression.Parameters);
  var sql = $"SELECT * FROM TestClass WHERE ${dbExpression.Filter} LIMIT ${dbExpression.Limit} OFFSET ${dbExpression.Offset} ORDER BY ${dbExpression.Sort}";
  var results = await connection.QueryAsync<TestClass>(sql, parameters);
  // do stuff
}
```

#### Dapper SimpleCRUD

```c#
using (var connection = await _connectionFactory.GetConnection())
{
  connection.Open();
  var parameters = new DynamicParameters(dbExpression.Parameters);
  var page = Utility.GetPage(dbExpression.Offset, dbExpression.Limit);
  var where = $"WHERE {dbExpression.Filter}";
  var results = (await connection.GetListPagedAsync<TestClass>(page,dbExpression.Limit, where, dbExpression.Sort, parameters)).ToList();
  // do stuff
}
```

### Common Customizations

```c#
public class SomeClass
{
  // prevents operations
  [RQL.NET.Ops.Disallowed("$like", "$eq")]
  // overrides column namer
  [RQL.NET.ColumnName("type")]
  // overrides (json) namer
  [RQL.NET.FieldName("type")]
  // ignores entirely
  [RQL.NET.Ignore.Sort]
  // prevents sorting
  [RQL.NET.Ignore]
  // prevents filtering
  [RQL.NET.Ignore.Filter]
  public string SomeProp { get; set; }
}
```

### RQL Operations

  | RQL Op  | SQL Op |           Json Types |
  | :------ | :----: | -------------------: |
  | `$eq`   |  `=`   | number, string, bool |
  | `$neq`  |  `!=`  | number, string, bool |
  | `$lt`   |  `<`   |         number, bool |
  | `$gt`   |  `>`   |         number, bool |
  | `$lte`  |  `<=`  |         number, bool |
  | `$gte`  |  `>=`  |         number, bool |
  | `$like` | `like` |             string[] |
  | `$in`   |  `in`  |   number[], string[] |
  | `$nin`  | `nin`  |   number[], string[] |
  | `$or`   |  `or`  |                   [] |
  | `$not`  | `not`  |                   {} |
  | `$and`  | `and`  |                   {} |
  | `$nor`  |   -    |                  n/a |

## Hackability

This library was structured to be a highly configurable parser. Most of the parser's components can be overriding directly or via a delegate or interface implementation via the [`Defaults`](RQL.NET/Defaults.cs) class. Most notably [`Defaults.DefaultConverter`](RQL.NET/Defaults.cs) and [`Defaults.DefaultValidator`](RQL.NET/DefaultTypeValidator.cs). Additionally many of the data structures and internal builders are exposed to enable this package to be used as a library. You could also implement a [custom class specification](RQL.NET/ClassSpecBuilder.cs), [field specification](RQL.NET/ClassSpecBuilder.cs), and [operation mapper](RQL.NET/IOpMapper.cs) to add pretty heavy customizations including custom types and operations.

```c#
public static class Defaults
{
    public static string SortSeparator = ",";
    public static string Prefix = "$";
    public static int Offset = 0;
    public static int Limit = 10000;
    public static Func<IParameterTokenizer> DefaultTokenizerFactory = () => new NamedTokenizer();
    public static Func<string, Type, object, IError> DefaultValidator = DefaultTypeValidator.Validate;
    public static Func<string, string> DefaultColumnNamer = x => x;
    public static Func<string, string> DefaultFieldNamer = x => {...};
    public static IOpMapper DefaultOpMapper = new SqlMapper();
    public static ClassSpecCache SpecCache = new InMemoryClassSpecCache();
    public static Func<string, Type, object, (object, IError)> DefaultConverter =
        (fieldName, type, raw) =>
        {
          ...
        };
}
```

## Note on Performance

The parser uses reflection and by **default** its done once per class and cached. Additionally when using the typed parse commands `Parse<T>(RqlExpression exp)` and `Parse(RqlExpression exp)` there is a redundant json serialization and then deserialization because this was built piggy backing off the JContainer tree structure from JSON.NET. To avoid this penalty use the `Parse<T>(string exp)` and `Parse(string exp)` calls.

## Release TODO

- [ ] better coverage
  - [ ] case: all attributes
  - [ ] case: all ops
- [ ] fix stricter validation - right side init object is and, or/nor is array
- [ ] fix empty object validation  ashtonian/RQL.NET#1
- [ ] share/publish

## vNext

- [ ] typed more actionable errors
- [ ] better C# side query building, complaints:
  - [ ] c# string multi-line literals not nice with interpolation
  - [ ] c# string multi-line literals require escaping double quotes
  - [ ] rqlExpression c# creation is heavy/bulky
  - [ ] c# dynamics don't like symbols in prop names
  - [ ] consider fluent action  ie rqlExpression.add(condition).add() or rqlExpression.and(rqlExpression).or(rqlExpression).
 - [ ] attributes
  - [ ] class level attributes
  - [ ] CustomTypeConverter
  - [ ] CustomValidator
  - [ ] DefaultSort
- [ ] integration tests
- [ ] nested "complex" data types support via joins or sub queries or..
- [ ] option to ignore validation
  - [ ] document removing of token prefix to potentially use with c# dynamic json literals easier
- [ ] consider adjusting output to be an expression tree that people can access for hackability
  - [ ] remove 3rd party dependency on JSON.NET, Initial leaf spec: {left, v, right, isField, iParse(RqlExpression exp)s there is a redundant Op, fieldSpecProperties...}
- [ ] Better Test coverage
  - [ ] Validators/converters
  - [ ] IEnumberable and [] types
- [ ] benchmark tests, run on PRs
- [ ] would be cool to generate part of a swagger documentation from a class spec..
- [ ] js client lib
- [ ] contributing guidelines and issue template
- [ ] official postgres / mongo support. Starting point: IOpMapper.
  - [ ] consider splitting package into RQL.Core and RQL.MSSQL to allow for RQL.Postgres or RQL.Mongo
