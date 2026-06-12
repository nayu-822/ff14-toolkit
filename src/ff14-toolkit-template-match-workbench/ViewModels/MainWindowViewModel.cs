using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using FF14Toolkit.App.Services.TemplateMatching;
using FF14Toolkit.TemplateMatchWorkbench.Infrastructure;
using FF14Toolkit.TemplateMatchWorkbench.Models;
using FF14Toolkit.TemplateMatchWorkbench.Services;

namespace FF14Toolkit.TemplateMatchWorkbench.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly TemplateProfileCatalog profileCatalog;
    private readonly ImageTemplateMatchService imageTemplateMatchService;
    private readonly string templatesRootPath;
    private readonly string inputImagesRootPath;
    private WorkbenchTemplateProfile? selectedTemplateProfile;
    private BitmapSource? originalImage;
    private BitmapSource? annotatedImage;
    private string? loadedImagePath;
    private string statusText;
    private string resultSummary;
    private bool isBusy;
    private string selectedTemplateDetails;

    public MainWindowViewModel()
        : this(new TemplateProfileCatalog(), new ImageTemplateMatchService())
    {
    }

    public MainWindowViewModel(
        TemplateProfileCatalog profileCatalog,
        ImageTemplateMatchService imageTemplateMatchService)
    {
        this.profileCatalog = profileCatalog;
        this.imageTemplateMatchService = imageTemplateMatchService;
        templatesRootPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Templates");
        inputImagesRootPath = Path.Combine(AppContext.BaseDirectory, "Assets", "InputImages");
        statusText = "テンプレート設定を読み込みます。";
        resultSummary = "まだマッチングは実行していません。";
        selectedTemplateDetails = string.Empty;

        TemplateProfiles = [];
        ScaleStatistics = [];
        RefreshProfiles();
    }

    public ObservableCollection<WorkbenchTemplateProfile> TemplateProfiles { get; }

    public ObservableCollection<TemplateScaleStatisticRow> ScaleStatistics { get; }

    public string TemplatesRootPath => templatesRootPath;

    public string InputImagesRootPath => inputImagesRootPath;

    public WorkbenchTemplateProfile? SelectedTemplateProfile
    {
        get => selectedTemplateProfile;
        set
        {
            if (SetProperty(ref selectedTemplateProfile, value))
            {
                SelectedTemplateDetails = value is null
                    ? "テンプレート設定を選択してください。"
                    : $"TemplateId: {value.TemplateId}{Environment.NewLine}Scales: {value.ScalesText}{Environment.NewLine}MinimumScore: {value.MinimumScore:F3}{Environment.NewLine}SampleStep: {value.SampleStep}{Environment.NewLine}CoarsePositionStep: {value.SearchOptions.CoarsePositionStep}{Environment.NewLine}CoarseSampleStep: {value.SearchOptions.CoarseSampleStep}{Environment.NewLine}RefineRadius: {value.SearchOptions.RefineRadius}{Environment.NewLine}MaximumRefineCandidates: {value.SearchOptions.MaximumRefineCandidates}";
            }
        }
    }

    public BitmapSource? OriginalImage
    {
        get => originalImage;
        private set => SetProperty(ref originalImage, value);
    }

    public BitmapSource? AnnotatedImage
    {
        get => annotatedImage;
        private set => SetProperty(ref annotatedImage, value);
    }

    public string? LoadedImagePath
    {
        get => loadedImagePath;
        private set => SetProperty(ref loadedImagePath, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public string ResultSummary
    {
        get => resultSummary;
        private set => SetProperty(ref resultSummary, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    public string SelectedTemplateDetails
    {
        get => selectedTemplateDetails;
        private set => SetProperty(ref selectedTemplateDetails, value);
    }

    public void RefreshProfiles()
    {
        TemplateProfiles.Clear();
        foreach (WorkbenchTemplateProfile profile in profileCatalog.LoadProfiles(templatesRootPath))
        {
            TemplateProfiles.Add(profile);
        }

        SelectedTemplateProfile = TemplateProfiles.FirstOrDefault();
        StatusText = TemplateProfiles.Count == 0
            ? $"テンプレート設定が見つかりません。{templatesRootPath} を確認してください。"
            : $"{TemplateProfiles.Count} 件のテンプレート設定を読み込みました。";
    }

    public void LoadImage(string imagePath)
    {
        LoadedImagePath = imagePath;
        OriginalImage = new BitmapImage(new Uri(imagePath));
        AnnotatedImage = OriginalImage;
        ResultSummary = "画像を読み込みました。マッチングを実行してください。";
        StatusText = $"読込画像: {Path.GetFileName(imagePath)}";
        ScaleStatistics.Clear();
    }

    public async Task ExecuteMatchAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedTemplateProfile is null)
        {
            StatusText = "テンプレート設定を選択してください。";
            return;
        }

        if (string.IsNullOrWhiteSpace(LoadedImagePath) || !File.Exists(LoadedImagePath))
        {
            StatusText = "先に検証対象画像を読み込んでください。";
            return;
        }

        IsBusy = true;
        StatusText = "マッチング実行中です。";

        try
        {
            WorkbenchMatchResult result = await imageTemplateMatchService.ExecuteAsync(
                LoadedImagePath,
                SelectedTemplateProfile,
                cancellationToken).ConfigureAwait(true);

            OriginalImage = result.OriginalImage;
            AnnotatedImage = result.AnnotatedImage;
            ScaleStatistics.Clear();
            foreach (TemplateScaleStatisticRow statistics in result.ScaleStatistics)
            {
                ScaleStatistics.Add(statistics);
            }

            ResultSummary =
                $"Status: {result.MatchResult.Status}{Environment.NewLine}" +
                $"BestScore: {result.MatchResult.BestScore:F3}{Environment.NewLine}" +
                $"Threshold: {result.MatchResult.Threshold:F3}{Environment.NewLine}" +
                $"Scale: {result.MatchResult.Scale:F2}{Environment.NewLine}" +
                $"MatchedBounds: {FormatBounds(result.MatchResult.MatchedBounds)}{Environment.NewLine}" +
                $"CandidateBounds: {FormatBounds(result.MatchResult.BestCandidateBounds)}{Environment.NewLine}" +
                $"ProcessingTime: {result.MatchResult.ProcessingTime.TotalMilliseconds:F1} ms{Environment.NewLine}" +
                $"TotalElapsed: {result.TotalElapsed.TotalMilliseconds:F1} ms{Environment.NewLine}" +
                $"Template: {Path.GetFileName(result.TemplateMetadata.Template)} ({result.TemplateMetadata.ReferenceTemplateWidth}x{result.TemplateMetadata.ReferenceTemplateHeight})";
            StatusText = "マッチングが完了しました。";
        }
        catch (OperationCanceledException)
        {
            StatusText = "マッチングをキャンセルしました。";
        }
        catch (Exception exception)
        {
            StatusText = $"マッチング失敗: {exception.Message}";
            ResultSummary = exception.ToString();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FormatBounds(System.Drawing.Rectangle? bounds)
    {
        return bounds is null
            ? "-"
            : $"{bounds.Value.Left}, {bounds.Value.Top}, {bounds.Value.Width}, {bounds.Value.Height}";
    }
}
