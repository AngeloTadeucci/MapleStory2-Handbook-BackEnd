﻿using Maple2Storage.Types;

namespace Maple2Storage.Tools;

public static class DotEnv {
    public static void Load() {
        string dotenv = Path.Combine(Paths.SolutionDir, ".env");

        if (!File.Exists(dotenv)) {
            throw new FileNotFoundException(".env file not found!");
        }

        foreach (string line in File.ReadAllLines(dotenv)) {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) {
                continue;
            }

            string[] parts = line.Split('=', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2) {
                continue;
            }

            Environment.SetEnvironmentVariable(parts[0], parts[1]);
        }
    }
}
