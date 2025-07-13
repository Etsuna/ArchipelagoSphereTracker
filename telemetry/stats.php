<?php
$dir = __DIR__ . '/telemetry';
$files = glob($dir . '/*.json');
sort($files); // plus anciens d'abord

$dates = [];
$dataByDate = [];
$programIds = [];
$latestPerProgram = [];

foreach ($files as $file) {
    $jsonContent = file_get_contents($file);
    $entries = json_decode($jsonContent, true);
    if (!is_array($entries)) continue;

    $date = basename($file, '.json');
    $dates[] = $date;

    foreach ($entries as $entry) {
        if (!isset($entry['id'])) continue;

        $id = $entry['id'];
        $programIds[$id] = true;

        $guildsCount = isset($entry['guilds']) ? (int)$entry['guilds'] : 0;
        $channelsCount = isset($entry['channels']) ? (int)$entry['channels'] : 0;

        if (!isset($dataByDate[$date])) {
            $dataByDate[$date] = [];
        }
        $dataByDate[$date][$id] = [
            'guilds' => $guildsCount,
            'channels' => $channelsCount
        ];

        // Enregistre la dernière donnée connue (on suppose que les fichiers sont triés du plus ancien au plus récent)
        $latestPerProgram[$id] = [
            'guilds' => $guildsCount,
            'channels' => $channelsCount,
            'date' => $date
        ];
    }
}

$programIds = array_keys($programIds);
sort($programIds);
$totalPrograms = count($programIds);

// Calculs finaux à partir des dernières valeurs
$totalGuilds = array_sum(array_column($latestPerProgram, 'guilds'));
$totalChannels = array_sum(array_column($latestPerProgram, 'channels'));
?>
<!DOCTYPE html>
<html lang="fr">
<head>
  <meta charset="UTF-8" />
  <title>Télémétrie ArchipelagoSphereTracker - Statistiques par programme</title>
  <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
  <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;600&display=swap" rel="stylesheet">
  <style>
    body {
      font-family: 'Inter', sans-serif;
      margin: 40px auto;
      max-width: 1500px;
      padding: 0 20px;
      background: #f8fafc;
      color: #1f2937;
    }

    h1, h2 {
      color: #111827;
    }

    h1 {
      font-size: 2em;
      margin-bottom: 0.5em;
    }

    h2 {
      margin-top: 2em;
      font-size: 1.5em;
    }

    p {
      margin: 0.5em 0;
    }

    select {
      padding: 0.5em;
      font-size: 1em;
      margin-top: 0.5em;
      margin-bottom: 1em;
      border: 1px solid #ccc;
      border-radius: 6px;
      background-color: #fff;
    }

    table {
      width: 100%;
      border-collapse: collapse;
      margin-top: 1em;
      background: #fff;
      border-radius: 8px;
      overflow: hidden;
      box-shadow: 0 2px 6px rgba(0, 0, 0, 0.05);
    }

    th, td {
      padding: 12px;
      text-align: left;
      border-bottom: 1px solid #e5e7eb;
    }

    th {
      background-color: #f1f5f9;
      font-weight: 600;
    }

    tr:hover {
      background-color: #f9fafb;
    }

    canvas {
      margin-top: 2em;
      background: #fff;
      padding: 10px;
      border-radius: 8px;
      box-shadow: 0 2px 8px rgba(0,0,0,0.05);
    }

    label {
      display: block;
      margin-top: 1em;
      font-weight: 600;
    }

    hr {
      margin: 2em 0;
      border: none;
      border-top: 1px solid #e5e7eb;
    }

    .table-wrapper {
      max-height: 300px; /* Ajuste ici la hauteur souhaitée */
      overflow-y: auto;
      border: 1px solid #e5e7eb;
      border-radius: 8px;
      box-shadow: inset 0 0 5px rgba(0, 0, 0, 0.05);
    }

    /* Fixe les entêtes pour la lisibilité si tu veux (facultatif) */
    .table-wrapper thead th {
      position: sticky;
      top: 0;
      background-color: #f1f5f9;
      z-index: 1;
    }
  </style>
</head>
<body>

<h1>Télémétrie ArchipelagoSphereTracker</h1>

<p><strong>Total de programmes uniques enregistrés :</strong> <?= $totalPrograms ?></p>
<p><strong>Total actuel des Guilds :</strong> <?= $totalGuilds ?></p>
<p><strong>Total actuel des Fils :</strong> <?= $totalChannels ?></p>

<h2>Détail par programme (dernière valeur connue)</h2>
<div class="table-wrapper">
  <table>
    <thead>
        <tr>
            <th>Programme</th>
            <th>Guilds</th>
            <th>Fils</th>
            <th>Dernière date</th>
        </tr>
    </thead>
    <tbody>
        <?php foreach ($latestPerProgram as $pid => $info): ?>
            <tr>
                <td><?= htmlspecialchars($pid) ?></td>
                <td><?= $info['guilds'] ?></td>
                <td><?= $info['channels'] ?></td>
                <td><?= htmlspecialchars($info['date']) ?></td>
            </tr>
        <?php endforeach; ?>
    </tbody>
      </table>
</div>
</table>

<hr>

<label for="programSelect">Sélectionner un programme :</label>
<select id="programSelect">
    <option value="all" selected>-- Tous les programmes --</option>
    <?php foreach ($programIds as $pid): ?>
        <option value="<?= htmlspecialchars($pid) ?>"><?= htmlspecialchars($pid) ?></option>
    <?php endforeach; ?>
</select>

<h2>Évolution dans le temps</h2>
<canvas id="telemetryChart" width="900" height="400"></canvas>

<script>
const dates = <?= json_encode($dates) ?>;
const dataByDate = <?= json_encode($dataByDate) ?>;

const ctx = document.getElementById('telemetryChart').getContext('2d');
let chart = null;

function updateChart(programId) {
    const guildsData = [];
    const channelsData = [];

    for (const date of dates) {
        const dayData = dataByDate[date] || {};

        if (programId === 'all') {
            let guildsSum = 0;
            let channelsSum = 0;
            for (const pid in dayData) {
                guildsSum += dayData[pid].guilds || 0;
                channelsSum += dayData[pid].channels || 0;
            }
            guildsData.push(guildsSum);
            channelsData.push(channelsSum);
        } else {
            if (dayData[programId]) {
                guildsData.push(dayData[programId].guilds);
                channelsData.push(dayData[programId].channels);
            } else {
                guildsData.push(null);
                channelsData.push(null);
            }
        }
    }

    const config = {
        type: 'line',
        data: {
            labels: dates,
            datasets: [
                {
                    label: `Guilds - ${programId === 'all' ? 'Tous les programmes' : programId}`,
                    data: guildsData,
                    borderColor: 'rgba(54, 162, 235, 1)',
                    backgroundColor: 'rgba(54, 162, 235, 0.2)',
                    fill: false,
                    tension: 0.1,
                    yAxisID: 'y',
                },
                {
                    label: `Fils - ${programId === 'all' ? 'Tous les programmes' : programId}`,
                    data: channelsData,
                    borderColor: 'rgba(255, 99, 132, 1)',
                    backgroundColor: 'rgba(255, 99, 132, 0.2)',
                    fill: false,
                    tension: 0.1,
                    yAxisID: 'y1',
                }
            ]
        },
        options: {
            responsive: true,
            interaction: {
                mode: 'index',
                intersect: false,
            },
            stacked: false,
            plugins: {
                title: {
                    display: true,
                    text: `Statistiques pour ${programId === 'all' ? 'tous les programmes' : 'le programme ' + programId}`
                }
            },
            scales: {
                y: {
                    type: 'linear',
                    display: true,
                    position: 'left',
                    title: {
                        display: true,
                        text: 'Guilds'
                    },
                    beginAtZero: true,
                    ticks: { stepSize: 1 }
                },
                y1: {
                    type: 'linear',
                    display: true,
                    position: 'right',
                    title: {
                        display: true,
                        text: 'Fils'
                    },
                    beginAtZero: true,
                    grid: { drawOnChartArea: false },
                    ticks: { stepSize: 1 }
                }
            }
        }
    };

    if(chart) {
        chart.destroy();
    }
    chart = new Chart(ctx, config);
}

document.getElementById('programSelect').addEventListener('change', e => {
    updateChart(e.target.value);
});

// Affichage initial avec tous les programmes
updateChart('all');
</script>

</body>
</html>
