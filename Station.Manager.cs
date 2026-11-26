namespace RadioServer.Stations;

public class StationManager
{
    private readonly Dictionary<string, RadioStation> _stations;

    public StationManager(IWebHostEnvironment env)
    {
        var audioRoot = Path.Combine(env.WebRootPath, "audio");
        _stations = new Dictionary<string, RadioStation>(StringComparer.OrdinalIgnoreCase)
        {
            ["rock"] = new RadioStation("rock", "Рок", Path.Combine(audioRoot, "rock")),
            ["jazz"] = new RadioStation("jazz", "Джаз", Path.Combine(audioRoot, "jazz")),
            ["pop"] = new RadioStation("pop", "Поп", Path.Combine(audioRoot, "pop")),
        };
    }

    public RadioStation? Get(string id) =>
        _stations.TryGetValue(id, out var s) ? s : null;

    public IEnumerable<object> GetStationsInfo() =>
        _stations.Values.Select(s => new
        {
            id = s.Id,
            name = s.Name,
            listeners = s.Listeners,
            currentTrack = s.CurrentTrackName
        });
}