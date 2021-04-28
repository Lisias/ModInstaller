
namespace ModInstaller
{
	public interface ISettings
	{
		string modFolder { get; set; }
		string APIFolder { get; set; }
		string installFolder { get; set; }
		string temp { get; set; }

		void Save();
	}
}
