// File: NormalizerTests.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Core.Merge;
using Xunit;

namespace ContactMesh.Core.Tests
{
    public sealed class NormalizerTests
    {
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("   ", "")]
        [InlineData("+1 (215) 555-0100", "2155550100")]
        [InlineData("+12675073489", "2675073489")]
        [InlineData("12675073489", "2675073489")]
        [InlineData("2675073489", "2675073489")]
        [InlineData("267-507-3489", "2675073489")]
        [InlineData("215 555 0100 x123", "2155550100")]
        [InlineData("215-555-0100 ext. 123", "2155550100")]
        public void PhoneNormalizer_Normalizes_For_Comparison(string? input, string expected)
        {
            var normalized = new PhoneNormalizer().NormalizeForComparison(input);

            Assert.Equal(expected, normalized);
        }

        [Theory]
        [InlineData("+12675073489", "267-507-3489")]
        [InlineData("12675073489", "267-507-3489")]
        [InlineData("2675073489", "267-507-3489")]
        [InlineData("267-507-3489", "267-507-3489")]
        [InlineData("4444", "4444")]
        public void PhoneNormalizer_Formats_Ten_Digit_Us_Number_For_Display(string? input, string expected)
        {
            var formatted = new PhoneNormalizer().FormatForDisplay(input);

            Assert.Equal(expected, formatted);
        }

        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("   ", "")]
        [InlineData(" Jane.Doe@Example.Org ", "jane.doe@example.org")]
        public void EmailNormalizer_Normalizes_For_Comparison(string? input, string expected)
        {
            var normalized = new EmailNormalizer().NormalizeForComparison(input);

            Assert.Equal(expected, normalized);
        }
    }
}
