using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Star.SqlData.Models;
using Xunit;

namespace Star.SqlData.Tests
{
    /// <summary>
    /// The unified response envelope is a public contract; its JSON shape must not drift.
    /// </summary>
    public class ApiResponseTests
    {
        [Fact]
        public void Success_UsesCode200AndKeepsData()
        {
            var payload = new Dictionary<string, object> { ["col_a"] = "value" };

            var response = ApiResponse.Success(payload);

            Assert.Equal("200", response.Code);
            Assert.Equal("Operation succeeded.", response.Message);
            Assert.Same(payload, response.Data);
        }

        [Fact]
        public void Success_AllowsCustomMessage()
        {
            var response = ApiResponse.Success(new { AffectedRows = 1 }, "Data created successfully");

            Assert.Equal("200", response.Code);
            Assert.Equal("Data created successfully", response.Message);
        }

        [Theory]
        [InlineData("400")]
        [InlineData("401")]
        [InlineData("403")]
        [InlineData("404")]
        [InlineData("500")]
        public void ErrorFactories_KeepTheirStatusCodes(string code)
        {
            ApiResponse response = code switch
            {
                "400" => ApiResponse.BadRequest(),
                "401" => ApiResponse.Unauthorized(),
                "403" => ApiResponse.Forbidden(),
                "404" => ApiResponse.NotFound(),
                _ => ApiResponse.ServerError(),
            };

            Assert.Equal(code, response.Code);
            Assert.False(string.IsNullOrWhiteSpace(response.Message));
            Assert.Null(response.Data);
        }

        [Fact]
        public void Error_CarriesCustomCodeAndMessage()
        {
            var response = ApiResponse.Error("429", "Too many requests");

            Assert.Equal("429", response.Code);
            Assert.Equal("Too many requests", response.Message);
        }

        [Fact]
        public void ToJson_EmitsCodeMsgDataProperties()
        {
            var json = JObject.Parse(ApiResponse.Success(new { col_a = "value" }, "ok").ToJson());

            Assert.Equal("200", (string?)json["code"]);
            Assert.Equal("ok", (string?)json["msg"]);
            Assert.Equal("value", (string?)json["data"]!["col_a"]);
            Assert.Null(json["data"]!["Code"]);
        }

        [Fact]
        public void ToJson_SerializesNullDataAsNull()
        {
            var json = JObject.Parse(ApiResponse.NotFound().ToJson());

            Assert.Equal(JTokenType.Null, json["data"]!.Type);
        }
    }
}
