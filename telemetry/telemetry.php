<?php
// === Réglages ===
$RATE_LIMIT_SECONDS = (int)(getenv('RATE_LIMIT_SECONDS') ?: 21600); // 6h par défaut

// === IP cliente (X-Forwarded-For > REMOTE_ADDR) ===
$ip = $_SERVER['HTTP_X_FORWARDED_FOR'] ?? ($_SERVER['REMOTE_ADDR'] ?? '');
$ip = explode(',', $ip)[0];
$now = time();

// === Whitelist d’IP via variable d’environnement ===
$allowIps = array_filter(array_map('trim', explode(',', getenv('ALLOW_IPS') ?: '')));
$isWhitelistedIp = in_array($ip, $allowIps, true);

// === Dossiers ===
$logDir = __DIR__ . '/telemetry_logs';
@mkdir($logDir, 0755, true);

// === Rate-limit fallback par IP (6h si non whitelistée) ===
if (!$isWhitelistedIp) {
    $ipFile = "$logDir/ip_" . md5($ip) . ".txt";
    if (file_exists($ipFile) && $now - (int)@file_get_contents($ipFile) < $RATE_LIMIT_SECONDS) {
        http_response_code(429);
        exit("⛔ Trop d'envois (limite IP: toutes les 6h)");
    }
    @file_put_contents($ipFile, (string)$now);
}

// === Filtrage User-Agent ===
$ua = $_SERVER['HTTP_USER_AGENT'] ?? '';
if (!str_starts_with($ua, 'ArchipelagoSphereTracker/')) {
    http_response_code(403);
    exit("⛔ User-Agent invalide");
}

// === Lecture JSON ===
$input = file_get_contents('php://input');
$data = json_decode($input, true);
if (!is_array($data)) {
    http_response_code(400);
    exit("⛔ Payload non valide");
}

// === Champs attendus ===
$required = ['id', 'timestamp', 'guilds', 'channels', 'version', 'astversion'];
foreach ($required as $f) {
    if (!isset($data[$f])) {
        http_response_code(400);
        exit("⛔ Champ manquant : $f");
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
$ts = strtotime($data['timestamp']);
if ($ts === false || $ts > time() + 3600) {
    http_response_code(400);
    exit("⛔ Timestamp invalide");
}

// === Rate-limit principal par ID (6h) ===
$idHash = md5((string)$data['id']);
$idFile = "$logDir/id_$idHash.txt";
if (file_exists($idFile) && $now - (int)@file_get_contents($idFile) < $RATE_LIMIT_SECONDS) {
    http_response_code(429);
    exit("⛔ Trop d'envois (limite par bot/id: toutes les 6h)");
}
@file_put_contents($idFile, (string)$now);

// === Stockage en JSONL ===
$storageFile = __DIR__ . '/telemetry_data.jsonl';
$entry = json_encode([
    'id' => $data['id'],
    'timestamp' => $data['timestamp'],
    'guilds' => (int)$data['guilds'],
    'channels' => (int)$data['channels'],
    'version' => $data['version'],
    'astversion' => $data['astversion'],
], JSON_UNESCAPED_SLASHES);
@file_put_contents($storageFile, $entry . "\n", FILE_APPEND);

http_response_code(200);
echo "✅ Télémétrie enregistrée";
