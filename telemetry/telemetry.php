<?php
// === Sécurité : rate limit par IP (1 envoi / 24h) ===
$ip = $_SERVER['REMOTE_ADDR'];
$now = time();
$logDir = __DIR__ . '/telemetry_logs';
@mkdir($logDir);
$logFile = "$logDir/ip_" . md5($ip) . ".txt";

if (file_exists($logFile)) {
    $last = intval(file_get_contents($logFile));
    if ($now - $last < 86400) {
        http_response_code(429);
        exit("⛔ Trop d'envois (1 fois par jour maximum)");
    }
}
file_put_contents($logFile, $now);

// === Filtrage du User-Agent ===
$ua = $_SERVER['HTTP_USER_AGENT'] ?? '';
if (!str_starts_with($ua, 'ArchipelagoSphereTracker/')) {
    http_response_code(403);
    exit("⛔ User-Agent invalide");
}

// === Lecture et validation du JSON ===
$input = file_get_contents('php://input');
$data = json_decode($input, true);

if (!is_array($data)) {
    http_response_code(400);
    exit("⛔ Payload non valide");
}

// === Vérification des champs attendus ===
$required = ['id', 'timestamp', 'guilds', 'channels', 'version', 'astversion'];
foreach ($required as $field) {
    if (!isset($data[$field])) {
        http_response_code(400);
        exit("⛔ Champ manquant : $field");
    }
}

// === Sanity checks ===
if (!is_numeric($data['guilds']) || $data['guilds'] < 0 || $data['guilds'] > 5000) {
    http_response_code(400);
    exit("⛔ Nombre de guildes invalide");
}

if (!is_numeric($data['channels']) || $data['channels'] < 0 || $data['channels'] > 10000) {
    http_response_code(400);
    exit("⛔ Nombre de channels invalide");
}

if (strtotime($data['timestamp']) === false || strtotime($data['timestamp']) > time() + 3600) {
    http_response_code(400);
    exit("⛔ Timestamp invalide");
}

// === Enregistrement en local (format JSON par ligne) ===
$storageFile = __DIR__ . '/telemetry_data.jsonl';
$entry = json_encode([
    'id' => $data['id'],
    'timestamp' => $data['timestamp'],
    'guilds' => (int) $data['guilds'],
    'channels' => (int) $data['channels'],
    'version' => $data['version'],
    'astversion' => $data['astversion'],
]);

file_put_contents($storageFile, $entry . "\n", FILE_APPEND);

http_response_code(200);
echo "✅ Télémétrie enregistrée";
