using Stack_Solver.Data.Repositories;
using Stack_Solver.Models.Jobs;
using System.Collections.ObjectModel;
using System.Windows;
using Wpf.Ui;

namespace Stack_Solver.ViewModels.Pages
{
    public partial class JobManagerViewModel : ObservableObject
    {
        private readonly IJobRepository _jobRepository;
        private readonly INavigationService _navigationService;
        private readonly PalletBuilderViewModel _palletBuilder;
        private bool _isInitialized;

        // Bumped on every selection so a slow detail load that has since been superseded is discarded.
        private int _detailsToken;

        /// <summary>Stored jobs, newest first.</summary>
        public ObservableCollection<JobRowViewModel> Jobs { get; } = [];

        /// <summary>The row selected in the grid; drives the details pane.</summary>
        [ObservableProperty]
        private JobRowViewModel? _selectedRow;

        /// <summary>Details of the selected job (SKUs, pallet, metadata), or null when nothing is selected.</summary>
        [ObservableProperty]
        private JobDetailsViewModel? _details;

        public bool HasDetails => Details is not null;

        public JobManagerViewModel(
            IJobRepository jobRepository,
            INavigationService navigationService,
            PalletBuilderViewModel palletBuilder)
        {
            _jobRepository = jobRepository;
            _navigationService = navigationService;
            _palletBuilder = palletBuilder;
            _jobRepository.JobAdded += OnJobAdded;
            _jobRepository.JobUpdated += OnJobUpdated;
        }

        /// <summary>
        /// Loads a job's full settings + results, hands them to the Pallet Builder, and navigates
        /// there so the run is mirrored and its default solution is shown instantly.
        /// </summary>
        public async Task OpenJobAsync(string id)
        {
            Job? job;
            try
            {
                job = await _jobRepository.GetAsync(id);
            }
            catch
            {
                return;
            }

            if (job is null || string.IsNullOrWhiteSpace(job.SettingsJson)) return;

            await _palletBuilder.OpenJobAsync(job);
            _navigationService.Navigate(typeof(Views.Pages.PalletBuilderPage));
        }

        // Selecting a row loads the full job (its settings/results JSON aren't in the grid summary)
        // and projects a details view model.
        partial void OnSelectedRowChanged(JobRowViewModel? value) => _ = LoadDetailsAsync(value?.Id);

        partial void OnDetailsChanged(JobDetailsViewModel? value) => OnPropertyChanged(nameof(HasDetails));

        private async Task LoadDetailsAsync(string? id)
        {
            int token = ++_detailsToken;
            if (id is null)
            {
                Details = null;
                return;
            }

            Job? job;
            try
            {
                job = await _jobRepository.GetAsync(id);
            }
            catch
            {
                return;
            }

            // A newer selection has taken over while we were loading; drop this stale result.
            if (token != _detailsToken) return;

            var settings = job is null ? null : JobSnapshotMapper.DeserializeSettings(job.SettingsJson);
            Details = job is not null && settings is not null ? new JobDetailsViewModel(job, settings) : null;
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            var summaries = await _jobRepository.GetSummariesAsync();
            Jobs.Clear();
            foreach (var summary in summaries)
                Jobs.Add(new JobRowViewModel(summary));

            // Preselect the newest job so the details pane is populated on first open.
            SelectedRow = Jobs.FirstOrDefault();
        }

        // A brand-new run: prepend it (summaries are newest-first). Deduplicate in case the
        // initial load raced with the add.
        private void OnJobAdded(object? sender, JobSummary summary) => OnUi(() =>
        {
            if (Jobs.Any(j => j.Id == summary.Id)) return;
            Jobs.Insert(0, new JobRowViewModel(summary));
        });

        // A status/result change: swap the row in place so the grid refreshes.
        private void OnJobUpdated(object? sender, JobSummary summary) => OnUi(() =>
        {
            for (int i = 0; i < Jobs.Count; i++)
            {
                if (Jobs[i].Id == summary.Id)
                {
                    bool wasSelected = ReferenceEquals(SelectedRow, Jobs[i]);
                    var row = new JobRowViewModel(summary);
                    Jobs[i] = row;
                    // Replacing the instance drops the grid's selection; restore it (which also
                    // refreshes the details pane) when the updated job was the selected one.
                    if (wasSelected) SelectedRow = row;
                    return;
                }
            }
            Jobs.Insert(0, new JobRowViewModel(summary));
        });

        private static void OnUi(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
                action();
            else
                dispatcher.BeginInvoke(action);
        }
    }

    /// <summary>Display projection of a <see cref="JobSummary"/> for the Job Manager grid.</summary>
    public sealed class JobRowViewModel(JobSummary summary)
    {
        public string Id => summary.Id;

        /// <summary>Run start, shown in local time.</summary>
        public string CreatedAtDisplay => summary.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        public JobStatus Status => summary.Status;
    }

    /// <summary>Read-only detail projection of a job for the Job Manager details pane.</summary>
    public sealed class JobDetailsViewModel
    {
        public JobStatus Status { get; }

        /// <summary>Run start, shown in local time to the second.</summary>
        public string CreatedAtDisplay { get; }

        /// <summary>Wall-clock generation time (completion − start), or "—" while still ongoing.</summary>
        public string DurationDisplay { get; }

        public int SolutionCount { get; }
        public int TotalPallets { get; }

        /// <summary>Pallet footprint and height, e.g. "120 × 80 × 14.4 cm".</summary>
        public string PalletSizeDisplay { get; }

        /// <summary>The SKUs that took part in the run, with their dimensions, weight and quantity.</summary>
        public IReadOnlyList<JobSkuDetail> Skus { get; }

        public JobDetailsViewModel(Job job, JobSettingsSnapshot settings)
        {
            Status = job.Status;
            CreatedAtDisplay = job.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            DurationDisplay = FormatDuration(job);
            SolutionCount = job.SolutionCount;
            TotalPallets = job.TotalPallets;
            PalletSizeDisplay = $"{settings.PalletLength} × {settings.PalletWidth} × {settings.PalletHeight:0.##} cm";
            Skus = [.. settings.Skus.Select(s => new JobSkuDetail(s))];
        }

        private static string FormatDuration(Job job)
        {
            if (job.CompletedAt is not DateTime completed) return "—";
            var elapsed = completed - job.CreatedAt;
            if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
            if (elapsed.TotalSeconds < 1) return $"{elapsed.TotalMilliseconds:0} ms";
            if (elapsed.TotalMinutes < 1) return $"{elapsed.TotalSeconds:0.0} s";
            return $"{(int)elapsed.TotalMinutes} m {elapsed.Seconds:00} s";
        }
    }

    /// <summary>One SKU row in the details pane.</summary>
    public sealed class JobSkuDetail(JobSkuSnapshot sku)
    {
        public string Name { get; } = sku.Name;
        public string Dimensions { get; } = $"{sku.Length}×{sku.Width}×{sku.Height}";
        public string Weight { get; } = $"{sku.Weight:0.##} kg";
        public int Quantity { get; } = sku.Quantity;
    }
}
