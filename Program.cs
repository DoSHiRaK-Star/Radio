using RadioServer.Stations;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<StationManager>();
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/stations", (StationManager mgr) =>
    Results.Json(mgr.GetStationsInfo()));

app.MapGet("/stream/{station}", async (string station, HttpContext ctx,
                                      StationManager mgr, ILogger<Program> log) =>
{
    var radio = mgr.Get(station);
    if (radio is null)
        return Results.NotFound($"Станция '{station}' не найдена");

    ctx.Response.ContentType = "audio/mpeg";
    ctx.Response.Headers["Cache-Control"] = "no-cache, no-store";
    ctx.Response.Headers["X-Accel-Buffering"] = "no";

    radio.AddListener();
    log.LogInformation("Подключился к {Station}. Всего: {Count}", station, radio.Listeners);

    var token = ctx.RequestAborted;
    const int bufferSize = 32 * 1024;
    var buffer = new byte[bufferSize];

    try
    {
        while (!token.IsCancellationRequested)
        {
            var (_, offset, filePath) = radio.GetCurrentPosition();

            FileStream fs;
            try
            {
                fs = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                                    FileShare.ReadWrite, bufferSize, useAsync: true);
            }
            catch (IOException ex)
            {
                log.LogWarning(ex, "Не удалось открыть {File}", filePath);
                radio.Advance(long.MaxValue);
                await Task.Delay(200, token);
                continue;
            }

            await using (fs)
            {
                if (offset > 0 && offset < fs.Length)
                    fs.Seek(offset, SeekOrigin.Begin);

                while (!token.IsCancellationRequested)
                {
                    int read;
                    try
                    {
                        read = await fs.ReadAsync(buffer.AsMemory(0, bufferSize), token);
                    }
                    catch (Exception ex) when (ex is IOException or OperationCanceledException)
                    {
                        log.LogWarning("Ошибка чтения {File}: {Msg}", filePath, ex.Message);
                        read = 0;
                    }

                    if (read == 0) break;

                    await ctx.Response.Body.WriteAsync(buffer.AsMemory(0, read), token);
                    await ctx.Response.Body.FlushAsync(token);
                    radio.Advance(read);
                }
            }

            radio.Advance(long.MaxValue);
        }
    }
    catch (OperationCanceledException) { }
    catch (IOException) { }
    finally
    {
        radio.RemoveListener();
        log.LogInformation("Отключился от {Station}. Всего: {Count}", station, radio.Listeners);
    }

    return Results.Empty;
});

app.Run();
