using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OptionView.Tests
{
    [TestClass]
    public class ExtensionsTests
    {
        [DataTestMethod]
        [DataRow("hello", 1, 3, "ell")]
        [DataRow("hello", 0, 5, "hello")]
        [DataRow("hello", 3, 10, "lo")]
        [DataRow("hi", 2, 1, "")]
        [DataRow("hi", 5, 1, "")]
        [DataRow("", 0, 3, "")]
        public void SafeSubstring_NeverThrowsAndClampsToInput(string input, int start, int length, string expected)
        {
            Assert.AreEqual(expected, input.SafeSubstring(start, length));
        }

        [TestMethod]
        public void Trim_ToMinute_DropsSecondsAndKeepsKind()
        {
            DateTime date = new DateTime(2024, 3, 15, 9, 31, 47, 250, DateTimeKind.Utc);

            DateTime trimmed = date.Trim(TimeSpan.TicksPerMinute);

            Assert.AreEqual(new DateTime(2024, 3, 15, 9, 31, 0, DateTimeKind.Utc), trimmed);
            Assert.AreEqual(DateTimeKind.Utc, trimmed.Kind);
        }

        [TestMethod]
        public void Trim_ToDay_DropsTimeOfDay()
        {
            DateTime date = new DateTime(2024, 3, 15, 23, 59, 59, DateTimeKind.Local);
            Assert.AreEqual(date.Date, date.Trim(TimeSpan.TicksPerDay));
        }
    }
}
