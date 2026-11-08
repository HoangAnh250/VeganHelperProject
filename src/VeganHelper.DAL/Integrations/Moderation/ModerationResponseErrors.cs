namespace VeganHelper.DAL.Integrations.Moderation;

public static class ModerationResponseErrors
{
    private static readonly HashSet<string> Codes = new(StringComparer.Ordinal)
    {
        "response_json_invalid", "response_envelope_invalid", "response_status_missing",
        "response_steps_missing", "response_step_invalid", "response_output_count",
        "response_output_missing", "response_verdict_json_invalid", "response_fields_missing",
        "response_fields_extra", "response_fields_duplicate", "response_field_type",
        "response_field_empty", "response_field_too_long", "response_reason_too_long",
        "response_verdict_invalid", "response_checks_missing", "response_checks_extra",
        "response_checks_invalid", "response_model_invalid", "response_usage_invalid",
        "response_incomplete", "response_part_invalid", "response_too_large", "response_upload_url_missing",
        "response_upload_url_invalid", "response_file_invalid", "response_file_mismatch",
        "response_file_state_invalid", "response_file_json_invalid", "response_result_invalid"
    };

    public static bool IsKnown(string? code) => code is not null && Codes.Contains(code);
}
