<?php
$dir = __DIR__ . '/telemetry';
$files = glob($dir . '/*.json');
krsort($files); // plus récents d'abord

$dates = [];
$dataByDate = [];
$programIds = [];
$totalGuilds = 0;
$totalChannels = 0;

foreach ($files as $file) {
    $jsonContent = file_get_contents($file);
    $entries = json_decode($jsonContent, true);
    if (!is_array($entries)) {
        continue;
    }

    $date = basename($file, '.json');
    $dates[] = $date;

    foreach ($entries as $entry) {
        if (!isset($entry['id'])) continue;

        $id = $entry['id'];
        $programIds[$id] = true;

        $guildsCount = isset($entry['guilds']) ? (int)$entry['guilds'] : 0;
        $channelsCount = isset($entry['channels']) ? (int)$entry['channels'] : 0;

        $totalGuilds += $guildsCount;
        $totalChannels += $channelsCount;

        if (!isset($dataByDate[$date])) {
            $dataByDate[$date] = [];
        }
        $dataByDate[$date][$id] = [
            'guilds' => $guildsCount,
            'channels' => $channelsCount
        ];
    }
}

rsort($dates); // du plus ancien au plus récent (pour graphique)
$programIds = array_keys($programIds);
sort($programIds);
$totalPrograms = count($programIds);
?>
<!DOCTYPE html>
<html lang="fr">
<head>
    <meta charset="UTF-8" />
    <title>Télémétrie ArchipelagoSphereTracker - Statistiques par programme</title>
    <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
</head>
<body>

<h1>Télémétrie ArchipelagoSphereTracker</h1>

<p><strong>Total de programmes uniques enregistrés :</strong> <?= $totalPrograms ?></p>
<p><strong>Total cumulé des Guilds :</strong> <?= $totalGuilds ?></p>
<p><strong>Total cumulé des Channels :</strong> <?= $totalChannels ?></p>

<label for="programSelect">Sélectionner un programme :</label>
<select id="programSelect">
    <option value="all" selected>-- Tous les programmes --</option>
    <?php foreach ($programIds as $pid): ?>
        <option value="<?= htmlspecialchars($pid) ?>"><?= htmlspecialchars($pid) ?></option>
    <?php endforeach; ?>
</select>

<h2>Statistiques Guilds & Channels</h2>
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
            // somme de tous les programmes
            let guildsSum = 0;
            let channelsSum = 0;
            for (const pid in dayData) {
                guildsSum += dayData[pid].guilds || 0;
                channelsSum += dayData[pid].channels || 0;
            }
            guildsData.push(guildsSum);
            channelsData.push(channelsSum);
        } else {
            // stats du programme spécifique
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
                    label: `Channels - ${programId === 'all' ? 'Tous les programmes' : programId}`,
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
                        text: 'Channels'
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
