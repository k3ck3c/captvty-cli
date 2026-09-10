# Captvty CLI

Interface en ligne de commande pour **Captvty 3.x**, destinée à piloter certaines fonctions de Captvty depuis **Mono sous Linux**.

Le projet fournit notamment :

- recherche d'émissions ;
- consultation détaillée des médias disponibles ;
- liste des émissions d'une chaîne ;
- téléchargement d'une émission ;
- cache local des programmes ;
- mise à jour partielle ou complète du cache ;
- timeout configurable pour éviter qu'un provider Captvty ne reste bloqué.

## Prérequis

- Linux
- Mono
- `mcs`
- Mono.Cecil
- une installation fonctionnelle de Captvty
- les fichiers :
  - `Captvty-cli-engine.exe`
  - `captvty-provider.exe`
  - `captvty-cli-v2.4.1.cs`

Exemple de dépendance Mono.Cecil sous Debian :

```text
/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll
```

## Compilation

```bash
mcs \
  -r:/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll \
  -out:captvty-cli.exe \
  captvty-cli-v2.4.1.cs
```

## Utilisation

```text
mono captvty-cli.exe [--timeout secondes] update [--all | "chaîne" ...]
mono captvty-cli.exe [--timeout secondes] search [--live] "texte"
mono captvty-cli.exe [--timeout secondes] list "chaîne" "texte"
mono captvty-cli.exe [--timeout secondes] info "texte"
mono captvty-cli.exe [--timeout secondes] info "chaîne" "texte"
mono captvty-cli.exe [--timeout secondes] get "chaîne" "titre" "sous-titre" high
```

### Timeout

`--timeout` est une option globale placée avant la commande.

```bash
mono captvty-cli.exe --timeout 30 list "TF1" "plus belle la vie"
```

`--timeout 0` désactive la limite de temps :

```bash
mono captvty-cli.exe --timeout 0 list "TF1" "plus belle la vie"
```

Valeurs par défaut :

- commandes générales : **120 secondes** ;
- `info "chaîne" "texte"` : **10 minutes** ;
- `get` : environ **12 heures**.

Une valeur fournie explicitement avec `--timeout` remplace le timeout par défaut de la commande.

## Mise à jour du cache

Sans argument, `update` met à jour les premières chaînes du catalogue :

```bash
mono captvty-cli.exe update
```

Mise à jour de chaînes précises :

```bash
mono captvty-cli.exe update "TF1" "France 2"
```

Mise à jour complète :

```bash
mono captvty-cli.exe update --all
```

Le cache est enregistré dans :

```text
captvty-cache.json
```

Lorsqu'une chaîne rencontre un timeout ou une erreur pendant `update`, son ancien contenu de cache est conservé.

## Recherche

### Recherche dans le cache

```bash
mono captvty-cli.exe search "plus belle la vie"
```

Si le cache existe, il est utilisé par défaut.

La recherche est insensible à la casse et aux accents.

### Recherche directe

Pour interroger directement les providers Captvty :

```bash
mono captvty-cli.exe search --live "plus belle la vie"
```

## Groupe TF1 : authentification

Pour les chaînes du groupe TF1, les identifiants sont fournis à la CLI par les variables d'environnement :

```bash
export TF1_USER="votre_utilisateur"
export TF1_PASS="votre_mot_de_passe"
```

La CLI utilise donc :

- `TF1_USER` : identifiant du compte TF1 ;
- `TF1_PASS` : mot de passe du compte TF1.

Il est préférable de ne pas inscrire ces identifiants directement dans le code source ni dans un fichier versionné sur GitHub.

Exemple :

```bash
export TF1_USER="mon_identifiant"
export TF1_PASS="mon_mot_de_passe"

mono captvty-cli.exe list "TF1" "plus belle la vie"
```

Pour éviter de saisir les variables à chaque session, elles peuvent par exemple être définies dans un fichier local non versionné ou dans l'environnement du shell.

## Liste des émissions d'une chaîne

```bash
mono captvty-cli.exe list "TF1" "plus belle la vie"
```

Exemple de sortie :

```text
Résultats sur TF1 pour "plus belle la vie" :
  1. Plus belle la vie, encore plus belle ...
  2. Plus belle la vie ...

485 émission(s)
```

Une recherche importante peut prendre près d'une minute ou davantage selon la chaîne et le réseau, d'où le timeout général fixé à 120 secondes.

## Informations détaillées

Recherche sur toutes les chaînes :

```bash
mono captvty-cli.exe info "plus belle la vie"
```

Recherche ciblée sur une chaîne :

```bash
mono captvty-cli.exe info "TF1" "plus belle la vie"
```

Les médias sont triés en privilégiant :

1. le média choisi par Captvty ;
2. la résolution ;
3. le bitrate.

## Téléchargement

```bash
mono captvty-cli.exe get "chaîne" "titre" "sous-titre" high
```

Exemple :

```bash
mono captvty-cli.exe get \
  "France 2" \
  "Titre de l'émission" \
  "Sous-titre" \
  high
```

Pendant le téléchargement, la CLI peut afficher l'état Captvty, le média sélectionné et le chemin du fichier final.

## Installation et fichiers utilisés

Placez `captvty-cli.exe`, `Captvty-cli-engine.exe` et `captvty-provider.exe`
dans le répertoire contenant `Captvty.exe`.

Le répertoire d'exécution contient donc notamment :

```text
Captvty.exe
Captvty-cli-engine.exe
captvty-provider.exe
captvty-cli.exe
captvty-cache.json       # créé par update
```

Les autres fichiers et DLL nécessaires à Captvty doivent naturellement rester
présents dans son installation.

`captvty-cache.json` est créé automatiquement lors d'une mise à jour.

## Interroger le cache avec jq

Le fichier `captvty-cache.json` peut aussi être consulté directement avec
[`jq`](https://jqlang.org/).

Afficher la date de dernière mise à jour :

```bash
jq -r '.updated' captvty-cache.json
```

Afficher le nombre total d'émissions présentes dans le cache :

```bash
jq '.programs | length' captvty-cache.json
```

Afficher la liste des chaînes présentes dans le cache :

```bash
jq -r '.programs[].channel' captvty-cache.json | sort -fu
```

Compter les émissions par chaîne :

```bash
jq -r '.programs[].channel' captvty-cache.json | sort | uniq -c | sort -nr
```

Afficher les titres et sous-titres d'une chaîne, par exemple TF1 :

```bash
jq -r '.programs[]
  | select(.channel == "TF1")
  | [.title, .subtitle]
  | @tsv' captvty-cache.json
```

Rechercher un texte dans les titres ou sous-titres, sans tenir compte de la
casse, par exemple `plus belle la vie` :

```bash
jq -r '.programs[]
  | select(
      ((.title // "") | test("plus belle la vie"; "i")) or
      ((.subtitle // "") | test("plus belle la vie"; "i"))
    )
  | [.channel, .title, .subtitle]
  | @tsv' captvty-cache.json
```

Afficher l'état de la dernière mise à jour des chaînes :

```bash
jq '.status' captvty-cache.json
```

Afficher uniquement les chaînes dont la dernière mise à jour n'est pas
marquée `ok` :

```bash
jq -r '.status
  | to_entries[]
  | select(.value != "ok")
  | [.key, .value]
  | @tsv' captvty-cache.json
```

## Codes de retour

Les principales valeurs utilisées sont :

- `0` : succès ;
- `1` : timeout ou échec d'une opération ;
- `2` : erreur d'utilisation, commande inconnue ou fichier requis absent ;
- `3` : cache absent ou vide pour une recherche locale.

Certains codes retournés par le worker Captvty peuvent aussi être propagés directement.

## Version 2.4.1

Principales évolutions :

- ajout de l'option globale `--timeout` ;
- `--timeout 0` pour une attente illimitée ;
- timeout général porté à **120 secondes** ;
- conservation de timeouts plus longs pour `info` ciblé et `get` ;
- mise à jour du cache robuste : en cas de timeout ou d'erreur sur une chaîne, l'ancien cache est conservé ;
- compatibilité avec les sorties worker V1 et V2.

## SHA-256

Source `captvty-cli-v2.4.1.cs` :

```text
5c82f099b9122a06f2d5dbedbbff810fcfa7f57c2879bf1478418ab2f50b6a07
```

## Remarque

Ce projet repose sur l'observation et l'appel d'éléments internes de Captvty. Les noms internes pouvant changer entre versions de Captvty, une nouvelle version de Captvty peut nécessiter une adaptation du moteur ou du worker.
