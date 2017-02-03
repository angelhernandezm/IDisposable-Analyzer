using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Temp {
	/// <summary>
	/// 
	/// </summary>
	class DisposableStats {
		/// <summary>
		/// Gets or sets the disposed count.
		/// </summary>
		/// <value>
		/// The disposed count.
		/// </value>
		public int DisposedCount {
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the disposable objects.
		/// </summary>
		/// <value>
		/// The disposable objects.
		/// </value>
		public int DisposableObjects {
			get;
			set;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DisposableStats"/> class.
		/// </summary>
		public DisposableStats() {

		}

		public DisposableStats(int numObjects) {
			DisposableObjects = numObjects;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DisposableStats"/> class.
		/// </summary>
		/// <param name="numObjects">The num objects.</param>
		/// <param name="count">The count.</param>
		public DisposableStats(int numObjects, int count) {
			DisposedCount = count;
			DisposableObjects = numObjects;
		}
	}

	/// <summary>
	/// 
	/// </summary>
	class MethodDescription {
		public string Container {
			get;
			set;
		}
		public string MethodName {
			get;
			set;
		}

		public MethodDescription(string container, string method) {
			Container = container;
			MethodName = method;
		}
	}

	/// <summary>
	/// 
	/// </summary>
	class Program {
		private static Dictionary<MethodDescription, DisposableStats> stats;

		/// <summary>
		/// Mains the specified args.
		/// </summary>
		/// <param name="args">The args.</param>
		static void Main(string[] args) {
			var assembly = string.Empty;
			stats = new Dictionary<MethodDescription, DisposableStats>();

			if (args.Length > 0 && !string.IsNullOrEmpty((assembly = CheckArgs(args)))) {
				try {
					var asm = Assembly.LoadFrom(assembly);
					Inspect(asm);
				} catch (Exception ex) {
					Console.Write(new string('*', 31));
					Console.Write("Exception");
					Console.Write(new string('*', 40));
					Console.WriteLine(ex.Message);
					Console.WriteLine(new string('*', 80));
				}

				DumpAnalysisResults();

				Console.WriteLine("\nPress any key to exit...");
				Console.ReadLine();
			}
		}


		/// <summary>
		/// Dumps the analysis results.
		/// </summary>
		private static void DumpAnalysisResults() {
			if (stats.Count > 0) {
				var index = 0;
				var disposed = stats.Count(x => x.Value.DisposedCount > 0);
				var disposable = stats.Count(x => x.Value.DisposableObjects > 0);

				Console.WriteLine(string.Format("Found: {0} methods.\nTotal of disposable objects: {1}\nObjects being disposed:{2}\n",
												new object[] { stats.Count, disposable, disposed }));

				Console.WriteLine(new string('-', 80));

				stats.ToList().ForEach(x => {
					Console.WriteLine(string.Format("Item: {0}\nContainer: {1}", new object[] { ++index, x.Key.Container }));
					Console.WriteLine(string.Format("Method: {0}", new object[] { x.Key.MethodName }));
					Console.WriteLine(string.Format("Disposable Objects Found {0} - Properly Disposed: {1}", new object[] { x.Value.DisposableObjects, x.Value.DisposedCount }));
					Console.WriteLine(new string('-', 80));
				});

			}
		}

		/// <summary>
		/// Checks the args.
		/// </summary>
		/// <param name="args">The args.</param>
		/// <returns></returns>
		private static string CheckArgs(string[] args) {
			var retval = string.Empty;

			var count = args.Count(x => x.Equals("-a", StringComparison.OrdinalIgnoreCase));

			if (count == 1 && args.Length >= 2 && args[0].Equals("-a", StringComparison.OrdinalIgnoreCase)) {
				if (File.Exists(args[1]))
					retval = args[1];
			}

			return retval;
		}

		/// <summary>
		/// Inspects the method.
		/// </summary>
		/// <param name="module">The module.</param>
		/// <param name="method">The method.</param>
		/// <param name="stats">The stats.</param>
		private static void InspectMethod(Module module, MethodInfo method) {
			var body = method.GetMethodBody();

			if (body != null) {
				var variables = body.LocalVariables;

				var disposable = variables.Where(x => x.LocalType.GetInterfaces()
										   .Where(y => !string.IsNullOrEmpty(y.Name) &&
												  y.Name.Equals("IDisposable")).FirstOrDefault() != null);

				var count = disposable.Count();

				var key = new MethodDescription(method.ReflectedType.ToString(), method.Name);

				if (count > 0 && !stats.ContainsKey(key))
					stats.Add(key, IdentifyDisposableObjects(body.GetILAsByteArray(), count, module));
			}
		}



		/// <summary>
		/// Reads the integer.
		/// </summary>
		/// <param name="ilasm">The ilasm.</param>
		/// <param name="position">The position.</param>
		/// <returns></returns>
		private static int ReadInteger(byte[] ilasm, ref int position) {
			return (((ilasm[position++] | (ilasm[position++] << 8)) | (ilasm[position++] << 0x10)) | (ilasm[position++] << 0x18));

		}

		/// <summary>
		/// Identifies the disposable objects.
		/// </summary>
		/// <param name="ilasm">The ilasm.</param>
		/// <param name="disposable">The disposable.</param>
		/// <returns></returns>
		private static DisposableStats IdentifyDisposableObjects(byte[] ilasm, int disposable, Module module) {
			int position = 0, offset = 0, token = 0;
			var retval = new DisposableStats(disposable);

			while (position < ilasm.Length) {
				var method = string.Empty;
				var value = ilasm[position++];

				// Is it a callvirt?
				if (value == 0x6F) {
					offset = position - 1;
					token = ReadInteger(ilasm, ref position);

					if ((!string.IsNullOrEmpty(method = module.ResolveMethod(token).Name)) &&
						 method.Equals("Dispose", StringComparison.OrdinalIgnoreCase))
						retval.DisposedCount++;

				}
			}
			return retval;
		}


		/// <summary>
		/// Inspects this instance.
		/// </summary>
		private static void Inspect(Assembly current) {
			var flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			var stats = new Dictionary<string, DisposableStats>();
			current.GetModules().ToList().ForEach(x => x.GetTypes().ToList().ForEach(y => y.GetMethods(flags).ToList().ForEach(z => InspectMethod(x, z))));
		}
	}
}