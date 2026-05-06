namespace Gameyogi.BuildChecklist.Editor.Evaluation
{
    public sealed class EvaluationOptions
    {
        public bool includeOpenScenes = true;
        public bool includeEnabledBuildSettingsScenes;

        public static EvaluationOptions Default => new EvaluationOptions();

        public static EvaluationOptions BuildPreprocess => new EvaluationOptions
        {
            includeOpenScenes = true,
            includeEnabledBuildSettingsScenes = true
        };
    }
}
