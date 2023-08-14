using System.Diagnostics;
using System.Threading.Tasks;

namespace Content.Server.Corvax.TTS;

public sealed partial class TTSSystem
{
    private Task<byte[]?> ApplySiliconFilter(byte[] data)
    {
        return RunFFMpeg(data, "-f wav -i pipe:0 -i ./SynthImpulse.wav -i ./RoomImpulse.wav aresample=44100 [re_1]; [re_1] apad=pad_dur=2 [in_1]; [in_1] asplit=2 [in_1_1] [in_1_2]; [in_1_1] [1] afir=dry=10:wet=10 [reverb_1]; [in_1_2] [reverb_1] amix=inputs=2:weights=8 1 [mix_1]; [mix_1] asplit=2 [mix_1_1] [mix_1_2]; [mix_1_1] [2] afir=dry=1:wet=1 [reverb_2]; [mix_1_2] [reverb_2] amix=inputs=2:weights=10 1 [mix_2]; [mix_2] equalizer=f=7710:t=q:w=0.6:g=-6,equalizer=f=33:t=q:w=0.44:g=-10 [out]; [out] alimiter=level_in=1:level_out=1:limit=0.5:attack=5:release=20:level=disabled -c:a libvorbis -b:a 64k -f ogg pipe:1");
    }

    private async Task<byte[]?> RunFFMpeg(byte[] data, string args)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        });

        if (process is null)
            return null;

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            return null;

        var result = (await process.StandardOutput.ReadToEndAsync()).Trim();
        return new byte[] { };
    }
}
