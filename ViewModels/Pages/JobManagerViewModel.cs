using Stack_Solver.Data.Repositories;
using Stack_Solver.Models.Jobs;
using System.Collections.ObjectModel;
using System.Windows;

namespace Stack_Solver.ViewModels.Pages
{
    public partial class JobManagerViewModel : ObservableObject
    {
        private readonly IJobRepository _jobRepository;
        private bool _isInitialized;

        /// <summary>Stored jobs, newest first.</summary>
        public ObservableCollection<JobRowViewModel> Jobs { get; } = [];

        public JobManagerViewModel(IJobRepository jobRepository)
        {
            _jobRepository = jobRepository;
            _jobRepository.JobAdded += OnJobAdded;
            _jobRepository.JobUpdated += OnJobUpdated;
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            var summaries = await _jobRepository.GetSummariesAsync();
            Jobs.Clear();
            foreach (var summary in summaries)
                Jobs.Add(new JobRowViewModel(summary));
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
                    Jobs[i] = new JobRowViewModel(summary);
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
}
