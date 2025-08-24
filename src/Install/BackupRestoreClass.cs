using ArchipelagoSphereTracker.src.Resources;

public class BackupRestoreClass : Declare
{
    public static Task Backup()
    {
        if (!Directory.Exists(ExtractPath))
        {
            Console.WriteLine(Resource.BRExternalFolderNotExists);
            return Task.CompletedTask;
        }

        if (!Directory.Exists(BackupPath))
        {
            Directory.CreateDirectory(BackupPath);
        }

        if (Directory.Exists(RomBackupPath))
        {
            Directory.Delete(RomBackupPath, true);
        }

        Directory.CreateDirectory(RomBackupPath);

        HashSet<string> extensionsRoms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".apworld", ".zip", ".7z",
            ".nes", ".sfc", ".smc", ".gba", ".gb", ".gbc",
            ".z64", ".n64", ".v64", ".nds", ".gcm", ".iso", ".cue", ".bin",
            ".gen", ".md", ".img", ".msu", ".pcm"
        };

        foreach (var fichier in Directory.GetFiles(ExtractPath, "*.*", SearchOption.TopDirectoryOnly))
        {
            string nomFichier = Path.GetFileName(fichier);

            if (nomFichier.Equals("README.md", StringComparison.OrdinalIgnoreCase))
                continue;

            string extension = Path.GetExtension(fichier);
            if (extensionsRoms.Contains(extension))
            {
                string cheminDestination = Path.Combine(RomBackupPath, nomFichier);

                try
                {
                    File.Move(fichier, cheminDestination, true);
                    Console.WriteLine(string.Format(Resource.BRMoved, nomFichier, cheminDestination));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format(Resource.BRError, fichier, ex.Message));
                }
            }
        }

        Console.WriteLine(Resource.BRRomBackupCompleted);

        if (Directory.Exists(CustomPath))
        {
            if (Directory.Exists(ApworldsBackupPath))
            {
                Directory.Delete(ApworldsBackupPath, true);
            }

            Directory.CreateDirectory(ApworldsBackupPath);

            foreach (var fichier in Directory.GetFiles(CustomPath, "*.apworld", SearchOption.TopDirectoryOnly))
            {
                string nomFichier = Path.GetFileName(fichier);
                string cheminDestination = Path.Combine(ApworldsBackupPath, nomFichier);
                try
                {
                    File.Move(fichier, cheminDestination, true);
                    Console.WriteLine(string.Format(Resource.BRMoved, nomFichier, cheminDestination));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format(Resource.BRError, fichier, ex.Message));
                }
            }

            Console.WriteLine(Resource.BRCustomWorldBackupCompleted);
        }


        if (Directory.Exists(PlayersPath))
        {
            if (Directory.Exists(PlayersBackup))
            {
                Directory.Delete(PlayersBackup, true);
            }

            Directory.CreateDirectory(PlayersBackup);

            foreach (var fichier in Directory.GetFiles(PlayersPath, "*", SearchOption.TopDirectoryOnly))
            {
                string nomFichier = Path.GetFileName(fichier);
                string destination = Path.Combine(PlayersBackup, nomFichier);
                File.Move(fichier, destination, overwrite: true);
                Console.WriteLine(string.Format(Resource.BRFileMoved, nomFichier));
            }

            foreach (var dossier in Directory.GetDirectories(PlayersPath, "*", SearchOption.TopDirectoryOnly))
            {
                string nomDossier = Path.GetFileName(dossier);
                if (string.Equals(nomDossier, "templates", StringComparison.OrdinalIgnoreCase))
                    continue;

                string destination = Path.Combine(PlayersBackup, nomDossier);

                if (Directory.Exists(destination))
                    Directory.Delete(destination, recursive: true);

                Directory.Move(dossier, destination);
                Console.WriteLine(string.Format(Resource.BRFolderMoved, nomDossier));
            }

            Console.WriteLine(Resource.BRPlayerFolderMoved);
        }

        return Task.CompletedTask;
    }

    public static Task RestoreBackup()
    {
        if (!Directory.Exists(BackupPath))
        {
            Console.WriteLine(Resource.BRBackupFolderNotExists);
            return Task.CompletedTask;
        }

        if (Directory.Exists(RomBackupPath))
        {
            foreach (var fichier in Directory.GetFiles(RomBackupPath, "*.*", SearchOption.TopDirectoryOnly))
            {
                string nomFichier = Path.GetFileName(fichier);
                string cheminDestination = Path.Combine(ExtractPath, nomFichier);
                try
                {
                    File.Move(fichier, cheminDestination, true);
                    Console.WriteLine(string.Format(Resource.BRRestoreMoved, nomFichier, cheminDestination));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format(Resource.BRError, fichier, ex.Message));
                }
            }
        }

        if (Directory.Exists(ApworldsBackupPath))
        {
            if (!Directory.Exists(CustomPath))
            {
                Directory.CreateDirectory(CustomPath);
            }

            foreach (var fichier in Directory.GetFiles(ApworldsBackupPath, "*.apworld", SearchOption.TopDirectoryOnly))
            {
                string nomFichier = Path.GetFileName(fichier);
                string cheminDestination = Path.Combine(CustomPath, nomFichier);
                try
                {
                    File.Move(fichier, cheminDestination, true);
                    Console.WriteLine(string.Format(Resource.BRRestoreMoved, nomFichier, cheminDestination));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format(Resource.BRError, fichier, ex.Message));
                }
            }
        }

        if (Directory.Exists(PlayersBackup))
        {
            foreach (var fichier in Directory.GetFiles(PlayersBackup, "*", SearchOption.TopDirectoryOnly))
            {
                string nomFichier = Path.GetFileName(fichier);
                string destination = Path.Combine(PlayersPath, nomFichier);
                File.Move(fichier, destination, overwrite: true);
                Console.WriteLine(string.Format(Resource.BRFileRestored, nomFichier));
            }
            foreach (var dossier in Directory.GetDirectories(PlayersBackup, "*", SearchOption.TopDirectoryOnly))
            {
                string nomDossier = Path.GetFileName(dossier);
                string destination = Path.Combine(PlayersPath, nomDossier);
                if (Directory.Exists(destination))
                    Directory.Delete(destination, recursive: true);
                Directory.Move(dossier, destination);
                Console.WriteLine(string.Format(Resource.BRFolderRestored, nomDossier));
            }
        }
        return Task.CompletedTask;
    }
}
