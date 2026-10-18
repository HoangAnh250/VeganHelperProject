using AutoMapper;
using VeganHelper.BLL.DTOs.HealthProfile;
using VeganHelper.BLL.DTOs.Shops;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Mapping;

public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<MealPlan, VeganHelper.BLL.DTOs.MealPlanning.MealPlanDto>()
            .ForMember(d=>d.Routine,o=>o.Ignore())
            .ForMember(d=>d.Menu,o=>o.Ignore());
        CreateMap<Flag, VeganHelper.BLL.DTOs.Posts.AdminPostReportDto>();
        CreateMap<PostModerationAssessment, VeganHelper.BLL.DTOs.Posts.ModerationAssessmentDto>();
        CreateMap<AdminAuditLog, VeganHelper.BLL.DTOs.Admin.AdminAuditLogDto>();
        CreateMap<AdminMemberProjection, VeganHelper.BLL.DTOs.Admin.AdminMemberDto>();
        CreateMap<AdminCategoryProjection, VeganHelper.BLL.DTOs.Admin.AdminCategoryDto>();
        CreateMap<AdminCommentReportProjection, VeganHelper.BLL.DTOs.Admin.AdminCommentReportDto>();
        CreateMap<ModerationPostProjection, VeganHelper.BLL.DTOs.Admin.ModerationPostDto>();
        CreateMap<ModerationFlagProjection, VeganHelper.BLL.DTOs.Admin.ModerationFlagDto>()
            .ForMember(d => d.Findings, o => o.MapFrom(s => ModerationMapping.FlagFindings(s.ReportedSnapshotJson, s.FindingsJson)))
            .ForMember(d => d.ConfidenceScore, o => o.MapFrom(s => ModerationMapping.FlagConfidence(s.ReportedSnapshotJson, s.ConfidenceScore)));
        CreateMap<PostModerationDecision, VeganHelper.BLL.DTOs.Admin.ModerationDecisionDto>();
        CreateMap<PostModerationSettings, VeganHelper.BLL.DTOs.Admin.ModerationSettingsDto>();
        CreateMap<PostMedia, VeganHelper.BLL.DTOs.Posts.PostMediaDto>();
        CreateMap<PostStep, VeganHelper.BLL.DTOs.Posts.PostStepDto>().ForMember(d => d.Instruction, o => o.MapFrom(s => s.Description));
        CreateMap<PostIngredient, VeganHelper.BLL.DTOs.Posts.PostIngredientDto>()
            .ForMember(d => d.Name, o => o.MapFrom(s => s.Ingredient == null ? "Unknown" : s.Ingredient.Name))
            .ForMember(d => d.Quantity, o => o.MapFrom(s => s.Quantity ?? 0));
        CreateMap<Post, VeganHelper.BLL.DTOs.Admin.ModerationPostDetailDto>(MemberList.None)
            .ForMember(d => d.Ingredients, o => o.MapFrom(s => s.PostIngredients))
            .ForMember(d => d.Steps, o => o.MapFrom(s => s.PostSteps.OrderBy(x => x.StepNumber)))
            .ForMember(d => d.Media, o => o.MapFrom(s => s.Media.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)));
        CreateMap<Notification, VeganHelper.BLL.DTOs.Notifications.NotificationDto>();
        CreateMap<UpdateHealthProfileRequest, UserProfile>(MemberList.None);
        CreateMap<UserProfile, HealthProfileDto>(MemberList.None);
        CreateMap<MealPlanRoutine, VeganHelper.BLL.DTOs.MealPlanning.MealRoutineDto>(MemberList.None);
        CreateMap<MealPlanEntry, VeganHelper.BLL.DTOs.MealPlanning.MealEntryDto>();
        CreateMap<AllergyProjection, AllergyDto>();
        CreateMap<Ingredient, IngredientOptionDto>();
        CreateMap<BmiHistory, BmiHistoryDto>()
            .ForMember(d => d.Timestamp, o => o.MapFrom(s => s.RecordedAt))
            .ForMember(d => d.Bmi, o => o.MapFrom(s => s.BmiValue));
        CreateMap<ShopOpeningPeriod, ShopOpeningPeriodDto>();
        CreateMap<Shop, ShopListDto>()
            .ForMember(d => d.ShopId, o => o.MapFrom(s => s.Id))
            .ForMember(d => d.DistanceKm, o => o.Ignore())
            .ForMember(d => d.IsOpenNow, o => o.Ignore());
        CreateMap<ShopProjection, ShopListDto>().IncludeMembers(s => s.Shop)
            .ForMember(d => d.DistanceKm, o => o.MapFrom(s => s.DistanceKm))
            .ForMember(d => d.IsOpenNow, o => o.Ignore());
        CreateMap<Shop, ShopDetailDto>().IncludeBase<Shop, ShopListDto>()
            .ForMember(d => d.GoogleMapsDirectionsUrl, o => o.Ignore())
            .ForMember(d => d.MediaUrls, o => o.MapFrom(s => s.Media.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => m.MediaUrl)))
            .ForMember(d => d.MenuItems, o => o.MapFrom(s => s.MenuItems.OrderBy(m => m.Name).ThenBy(m => m.Id).Select(m => m.Name)));
    }
}
