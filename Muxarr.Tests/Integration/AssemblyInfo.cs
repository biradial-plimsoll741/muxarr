using Microsoft.VisualStudio.TestTools.UnitTesting;

// Integration tests touch real files in a shared temp dir and invoke real
// ffmpeg / mkvmerge / ffprobe / mkvpropedit binaries. Static process-kill
// helpers (FFmpeg.KillExistingProcesses / MkvMerge.KillExistingProcesses)
// enumerate by name across the whole system, so parallel test execution
// against real tool processes is unsafe.
[assembly: DoNotParallelize]
