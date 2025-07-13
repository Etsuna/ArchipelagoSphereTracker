<?php
// Reçoit les données JSON POSTées
$input = file_get_contents("php://input");
$data = json_decode($input, true);

if (!$data || !isset($data['id'])) {
    http_response_code(400);
    echo "Aucune donnée JSON valide reçue";
    exit;
}

$dir = __DIR__ . '/telemetry';
if (!file_exists($dir)) {
    mkdir($dir, 0777, true);
}

$filename = $dir . '/' . date('Y-m-d') . '.json';

// Charge les données existantes
$existingData = [];
if (file_exists($filename)) {
    $jsonContent = file_get_contents($filename);
    $existingData = json_decode($jsonContent, true);
    if (!is_array($existingData)) {
        $existingData = [];
    }
}

// Ajoute la nouvelle entrée
$existingData[] = $data;

// Sauvegarde tout le tableau dans le fichier
file_put_contents($filename, json_encode($existingData, JSON_PRETTY_PRINT));

http_response_code(200);
echo "Télémétrie reçue avec succès";
