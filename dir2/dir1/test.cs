using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using Xamarin.EngineeringServices.Host;
using System.Text.RegularExpressions;
using System.Linq;
using Xamarin.EngineeringServices.Git;

// sdfiuhfsiudhf
// dsiufhsdiufh

namespace Xamarin.EngineeringServices.RepoGraphs
{
	public static class RepoDiscoverer
	{
		class RepoInfo
		{
			public string Revision;
			public RepoReference[] References;
			public Task QueryTask;
		}

		static Dictionary<string, RepoInfo> repoCache = new Dictionary<string, RepoInfo> ();

		static string GetKey (SeriesRepo repo)
		{
			return repo.Location.ToShortName () + " " + repo.TargetBranch;
		}

		public static async Task FillReferences (IOperationContext monitor, CheckSettings settings, SeriesRepo repo, bool updateCache = true)
		{
			var repoRev = await repo.GetCurrentRevision ();

			RepoInfo ri;

			TaskCompletionSource<bool> newTask = null;

			lock (repoCache) {
				var key = GetKey (repo);
				if (!repoCache.TryGetValue (key, out ri) || ri.Revision != repoRev) {
					newTask = new TaskCompletionSource<bool> ();
					ri = new RepoInfo {
						Revision = repoRev,
						QueryTask = newTask.Task
					};
					if (updateCache)
						repoCache [key] = ri;
				}
			}

			repo.ClearReferences ();

			if (newTask != null) {
				// Not found in cache
				try {
					//var t1 = RepoExternalReference.FillReferences (monitor, repo);
					var t2 = RepoSubmoduleReference.FillReferences (monitor, settings, repo);
					var t3 = RepoRegexReference.FillVersionChecksReferences (monitor, settings, repo);
					var t4 = RepoRegexReference.FillReadmeFileReferences (monitor, settings, repo);
					var t5 = RepoRegexReference.FillMonoTouchHashReferences (monitor, settings, repo);
					var t6 = RepoRegexReference.FillMonoDroidHashReferences (monitor, settings, repo);
					var t7 = RepoRegexReference.FillMaciosHashReferences(monitor, settings, repo);					
					var t8 = RepoRegexReference.FillExternalReferences(monitor, settings, repo);
					var t9 = MdAddinReference.FillMdAddinReferences(monitor, settings, repo);
					//var t6 = RepoRegexReference.FillAndroidVSHashReferences (monitor, repo);
					await Task.WhenAll (t2, t3, t4, t5, t6, t7, t8, t9);

					var tasks = new List<Task> ();
					foreach (var r in repo.References) {
						r.IsLoaded = true;
						var rc = r;
						tasks.Add (rc.LoadReference (monitor, settings, repo));//.ContinueWith (t => rc.CalcCommitDistance (monitor), TaskContinuationOptions.NotOnFaulted));
					}
					await Task.WhenAll (tasks);

					if (updateCache) {
						lock (repoCache) {
							ri.References = repo.References.Select (r => r.Clone ().Reset ()).ToArray ();
						}
					}
				} finally {
					newTask.SetResult (true);
				}
			} else {
				// Found in canche
				foreach (var r in ri.References)
					repo.AddReference (r.Clone ());
			}
		}

		public static async Task<RepoReference[]> DiscoverReferences (IOperationContext monitor, string url, string rev, Git.IGitRepositoryManager repositoryManager = null)
		{
			var repo = new SeriesRepo {
				Location = RepositoryLocation.FromUrl (url),
				TargetRevision = rev
			};
			repo.BeginUpdate ();
			try {
				var settings = new CheckSettings ();
				settings.RepoContext = new RepoContext ();
				if (repositoryManager != null)
					settings.GitRepositoryManager = repositoryManager;
				repo.CalculateCurrentRevision (monitor, settings);
				await FillReferences (monitor, settings, repo, false);
			} finally {
				repo.EndUpdate ();
			}
			return repo.References.ToArray ();
		}
	}
}


