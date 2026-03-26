using GoFibreSpeedTester.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GoFibreSpeedTester.Tests;

[TestClass]
public sealed class ValueParsingTests
{
  [TestMethod]
  public void ParseNullableDouble_Works()
  {
    Assert.AreEqual(123.45, ValueParsing.ParseNullableDouble("123.45")!.Value, 0.0001);
    Assert.AreEqual(123.45, ValueParsing.ParseNullableDouble("123,45")!.Value, 0.0001);
    Assert.AreEqual(987.6, ValueParsing.ParseNullableDouble("Download 987.6 Mbps")!.Value, 0.0001);
    Assert.AreEqual(12.0, ValueParsing.ParseNullableDouble("Ping: 12 ms")!.Value, 0.0001);
    Assert.IsNull(ValueParsing.ParseNullableDouble(""));
    Assert.IsNull(ValueParsing.ParseNullableDouble(null));
  }
}

