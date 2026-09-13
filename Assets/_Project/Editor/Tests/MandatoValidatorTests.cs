using Mandato.Editor;
using NUnit.Framework;

namespace Mandato.Editor.Tests
{
    [TestFixture]
    public class MandatoValidatorTests
    {
        [Test]
        public void ValidateAllContent_RunsAndReturnsZeroErrorsForCleanProject()
        {
            int errors = MandatoValidator.ValidateAllContent();
            Assert.AreEqual(0, errors, $"ValidateAllContent encontrou {errors} erro(s) de catálogo.");
        }

        [Test]
        public void ValidatePipelineCI_ReturnsTrueForCleanProject()
        {
            bool passed = MandatoValidator.ValidatePipelineCI();
            Assert.IsTrue(passed, "ValidatePipelineCI falhou no projeto limpo.");
        }
    }
}
