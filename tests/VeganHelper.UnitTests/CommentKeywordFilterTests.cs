using System.Text;
using VeganHelper.BLL.Services;

namespace VeganHelper.UnitTests;

public sealed class CommentKeywordFilterTests
{
    [Theory]
    [InlineData("con đĩ mẹ mày")]
    [InlineData("con đỉ mẹ mày")]
    [InlineData("CON ĐỈ MẸ MÀY!")]
    [InlineData("  con   đỉ   mẹ mày  ")]
    [InlineData("đĩ")]
    [InlineData("đỉ")]
    public void Evaluate_HoiNgaVariantsHaveTheSameHiddenAction(string text)
    {
        var filter = new CommentKeywordFilter();

        foreach (var form in new[] { NormalizationForm.FormC, NormalizationForm.FormD })
        {
            var result = filter.Evaluate(text.Normalize(form));
            Assert.Equal(CommentModerationAction.HidePendingReview, result.Action);
            Assert.NotNull(result.MatchedKeyword);
        }
    }

    [Theory]
    [InlineData("Mình đi chợ mua nấm")]
    [InlineData("Đi")]
    [InlineData("Con đỉa")]
    [InlineData("Hạt dẻ và củ cải")]
    [InlineData("Đậu hũ và đu đủ")]
    [InlineData("Cắt quả này khó vcl")]
    public void Evaluate_PreservesSafeWordsAndWholeWordBoundaries(string text)
    {
        var filter = new CommentKeywordFilter();

        foreach (var form in new[] { NormalizationForm.FormC, NormalizationForm.FormD })
            Assert.Equal(CommentModerationAction.Allow, filter.Evaluate(text.Normalize(form)).Action);
    }
}
