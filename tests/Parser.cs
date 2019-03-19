using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace tests
{
    public class TestClass
    {
        public long T_Long { get; set; }
        public float T_Float { get; set; }
        public int T_Int { get; set; }
        public string T_String { get; set; }
        public bool T_Bool { get; set; }
        //   public DateTime T_DateTime { get; set; }
        // public TestClass2 T_SubClass { get; set; }
    }

    public class TestClass2
    {
        public int X_Int { get; set; }
        public string X_String { get; set; }
        public bool X_Bool { get; set; }
        public DateTime X_DateTime { get; set; }
    }

    public class ParserTests
    {
        [Fact]
        public void SimpleAnd()
        {
            var raw = @"{ 
                        ""t_Long"": 3,
                        ""t_Int"": 2, 
                        ""t_Float"": 3.4, 
                        ""t_String"": ""str"", 
                        ""t_Bool"": true
                		}
                  ";
            var (result, errs) = Parser.Parse<TestClass>(raw);
            Assert.True(errs == null);
            Assert.Equal("T_Long = @t_Long AND T_Int = @t_Int AND T_Float = @t_Float AND T_String = @t_String AND T_Bool = @t_Bool", result.FilterExpression);
        }

        [Fact]
        public void Complex()
        {
            var raw = @"{ 
                            ""t_Long"": 3,
                            ""$or"" : [ 
                                {""t_Long"" : {""$gte"" : 1 }},
                                {""t_Long"" : {""$lte"" : 5 }},
                                {""t_Long"" : 20 }
                            ],
                            ""t_Bool"": true,
                            ""$and"" : [
                                {""t_String"": {""$like"":""%testing%""}},                                
                                {""t_String"": {""$like"":""%testing2%""}} 
                            ]
                		}
                  ";

            var (result, errs) = Parser.Parse<TestClass>(raw);
            Assert.True(errs == null);
            Assert.Equal("T_Long = @t_Long AND ( T_Long >= @t_Long2 OR T_Long <= @t_Long3 OR T_Long = @t_Long4 ) AND T_Bool = @t_Bool AND ( T_String LIKE @t_String AND T_String LIKE @t_String2 )", result.FilterExpression);
            Console.WriteLine(result.FilterExpression);
        }

    }
}



