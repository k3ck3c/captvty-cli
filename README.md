# Captvty CLI sous Linux / Mono

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
- `jq` (optionnel, pour interroger directement `captvty-cache.json`)
- une installation originale de **Captvty 3.0.1.26**

Sous Debian, les dépendances nécessaires peuvent être installées par exemple avec :

```bash
sudo apt install mono-devel libmono-cecil-private-cil
```

Pour installer également `jq` :

```bash
sudo apt install jq
```

Mono.Cecil peut être installé dans une version différente suivant la version de Debian.

Le chemin peut être déterminé automatiquement :

```bash
CECIL=$(find /usr/lib/mono/gac/Mono.Cecil \
  -name Mono.Cecil.dll \
  | sort -V \
  | tail -1)

echo "$CECIL"
```

Par exemple, le test sur Debian 12 utilise Mono.Cecil `0.11.0.0`, alors qu'une installation Debian 13 peut disposer de `0.11.1.0`.

## Construction depuis les sources

Le dépôt ne fournit pas `Captvty.exe`.

Après avoir cloné le dépôt, décompressez Captvty 3.0.1.26 Windows original directement dans le répertoire du clone.

Par exemple :
```bash
git clone https://github.com/k3ck3c/captvty-cli.git
cd captvty-cli
unzip /chemin/vers/captvty-3.0.1.26.zip
```

Après extraction, le répertoire doit notamment contenir :

Captvty.exe

Captvty.exe.config

bin/

Vidéos/


Le répertoire `Vidéos/`, créé lors de l'extraction de Captvty, est utilisé pour accueillir les émissions téléchargées.


Le fichier d'entrée doit être le `Captvty.exe` **Windows original de Captvty 3.0.1.26**. N'utilisez pas une version déjà modifiée ou adaptée pour Mono.

Les patches Cecil utilisent certains noms internes obfusqués de Captvty 3.0.1.26. Ils ne sont donc pas garantis compatibles avec une autre version de Captvty.

Les autres fichiers de l'installation originale, notamment le répertoire `bin/`, doivent être conservés pour l'exécution de la CLI.

La chaîne de construction du moteur est la suivante :

```text
Captvty.exe
    │
    │ patch-mono-compat
    ▼
Captvty-mono-clean.exe
    │
    │ patch-captvty-cli
    ▼
Captvty-cli-engine-unpatched.exe
    │
    │ patch-bfmtv-hashset
    ▼
Captvty-cli-engine.exe
    │
    │ patch-m6
    ▼
Captvty-cli-engine-m6.exe
```

`Captvty-cli-engine.exe` est le moteur utilisé par défaut par la CLI.

### 1. Déterminer le chemin de Mono.Cecil

```bash
CECIL=$(find /usr/lib/mono/gac/Mono.Cecil \
  -name Mono.Cecil.dll \
  | sort -V \
  | tail -1)

echo "$CECIL"
```

### 2. Compiler les outils de patch

Compiler le patch de compatibilité Mono :

```bash
mcs \
  -r:$CECIL \
  -r:System.Windows.Forms \
  -r:System.Drawing \
  -out:patch-mono-compat.exe \
  patch-mono-compat.cs
```

Compiler le patch du moteur CLI :

```bash
mcs \
  -r:$CECIL \
  -out:patch-captvty-cli.exe \
  patch-captvty-cli.cs
```

Compiler le patch M6 :

```bash
mcs \
  -r:$CECIL \
  -out:patch-m6.exe \
  patch-m6.cs
```

Compiler le correctif BFMTV :

```bash
mcs \
  -r:$CECIL \
  -out:patch-bfmtv-hashset.exe \
  patch-bfmtv-hashset.cs
```

### 3. Construire le moteur compatible Mono

À partir du `Captvty.exe` Windows original de Captvty 3.0.1.26 :

```bash
mono patch-mono-compat.exe \
  Captvty.exe \
  Captvty-mono-clean.exe
```

Le programme doit notamment afficher :

```text
Créé : Captvty-mono-clean.exe
```

Construire ensuite le moteur utilisé par la CLI :

```bash
mono patch-captvty-cli.exe \
  Captvty-mono-clean.exe \
  Captvty-cli-engine-unpatched.exe
```

Le résultat attendu comprend notamment :

```text
Patch : System.Void _exB::_GUA(_8eB)
Patch : System.Void _exB::_XFb()
OK : moteur CLI écrit dans Captvty-cli-engine-unpatched.exe
```

Appliquer ensuite le correctif BFMTV :

```bash
mono patch-bfmtv-hashset.exe \
  Captvty-cli-engine-unpatched.exe \
  Captvty-cli-engine.exe
```

Le résultat attendu comprend notamment :

```text
patched=1
written: Captvty-cli-engine.exe
```

`patched=1` confirme que le correctif attendu a été appliqué exactement une fois.

`Captvty-cli-engine.exe` est alors le moteur utilisé par défaut par la CLI.

Le patch M6 peut ensuite être appliqué si nécessaire :

```bash
mono patch-m6.exe \
  Captvty-cli-engine.exe \
  Captvty-cli-engine-m6.exe
```

Il produit une variante :

```text
Captvty-cli-engine-m6.exe
```

La CLI utilise par défaut `Captvty-cli-engine.exe`.

### 4. Compiler le provider

```bash
mcs \
  -out:captvty-provider.exe \
  captvty-provider.cs
```

### 5. Compiler les shims CefSharp

```bash
mcs -target:library \
  -out:CefSharp.dll \
  CefSharp-stub.cs

mcs -target:library \
  -out:CefSharp.Core.dll \
  -r:CefSharp.dll \
  -r:System.Net.Http.dll \
  CefSharp-Core-stub.cs

mcs -target:library \
  -out:CefSharp.WinForms.dll \
  -r:CefSharp.dll \
  -r:CefSharp.Core.dll \
  CefSharp-WinForms-stub.cs
```

Le warning `CS0067` concernant JavascriptObjectRepository.ResolveObject peut être ignoré.

Les trois DLL produites doivent rester dans le répertoire courant, à côté de captvty-cli.exe et captvty-provider.exe.

### 6. Compiler la CLI

```bash
mcs \
  -r:$CECIL \
  -out:captvty-cli.exe \
  captvty-cli-v2.5.0.cs
```

À l'issue de la compilation, le répertoire d'exécution contient notamment :

```text
Captvty.exe
Captvty-cli-engine.exe
Captvty-cli-engine-m6.exe
captvty-provider.exe
captvty-cli.exe
CefSharp.dll
CefSharp.Core.dll
CefSharp.WinForms.dll
bin/
```


Les autres DLL et fichiers nécessaires à Captvty doivent rester présents dans son installation.

### Validation de la construction

La chaîne complète de construction a été testée depuis un clone propre du dépôt sur **Debian 12**, à partir du `Captvty.exe` Windows original de Captvty 3.0.1.26.

Avant le premier test :

```bash
export MONO_PATH="$PWD:$PWD/bin"
```

Par exemple :

```bash
mono captvty-cli.exe --timeout 60 list "France 2" "journal"
```

## Utilisation

Avant de lancer la CLI, définissez `MONO_PATH` pour permettre à Mono de trouver les assemblies de Captvty, notamment celles de CefSharp.

Depuis le répertoire d'installation de Captvty :

```bash
export MONO_PATH="$PWD:$PWD/bin"
```

Les principales commandes sont :

```bash
mono captvty-cli.exe [--timeout secondes] update [--all | "chaîne" ...]
mono captvty-cli.exe [--timeout secondes] search [--live] "texte"
mono captvty-cli.exe [--timeout secondes] list "chaîne" "texte"
mono captvty-cli.exe [--timeout secondes] info "texte"
mono captvty-cli.exe [--timeout secondes] info "chaîne" "texte"
mono captvty-cli.exe [--timeout secondes] get "chaîne" "titre" "sous-titre" high
```

## Timeout

`--timeout` est une option globale placée avant la commande.

Exemple :

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

Sans argument, `update` met à jour les 10 premières chaînes du catalogue, affiche le nombre de replays par chaine et dure une dizaine de minutes :

```bash
mono captvty-cli.exe update
```

```text
[1/10] TF1 ... OK (15416)
[2/10] France 2 ... OK (3303)
[3/10] France 3 ... OK (10092)
[4/10] France 4 ... OK (2734)
[5/10] France 5 ... OK (1893)
[6/10] M6 ... OK (5563)
[7/10] Arte ... OK (14246)
[8/10] LCP Assemblée Nationale ... OK (1384)
[9/10] Public Sénat ...  OK (872)
[10/10] W9 ... OK (2311)

Catalogue: 57814 émission(s)
```

Mise à jour de chaînes précises :

```bash
mono captvty-cli.exe update "TF1" "France 2"
```
exemple

<pre>
mono captvty-cli.exe update "LCI"
<strong>[1/1] LCI ... OK (3065)</strong>
</pre>

Mise à jour complète :

```bash
mono captvty-cli.exe update --all
```
```text
[1/54] TF1 ... OK (14041)
[2/54] France 2 ... OK (3303)
[3/54] France 3 ... OK (10092)
[4/54] France 4 ... OK (2736)
[5/54] France 5 ... OK (1894)
[6/54] M6 ...  OK (5563)
[7/54] Arte ... OK (14246)
[8/54] LCP Assemblée Nationale ... OK (1252)
[9/54] Public Sénat ... OK (900)
[10/54] W9 ... OK (2311)
[11/54] TMC ... OK (1429)
[12/54] TFX ... OK (1697)
[13/54] Gulli ... OK (2020)
[14/54] BFMTV ... OK (3887)
[15/54] CNews ... erreur (134) (ancien cache conservé)
[16/54] LCI ... OK (3065)
[17/54] Franceinfo ... OK (346)
[18/54] CStar ...  erreur (134) (ancien cache conservé)
[19/54] T18 ... OK (0)
[20/54] NOVO19 ... OK (0)
[21/54] TF1 Séries Films ... OK (490)
[22/54] La chaîne L'Équipe ... OK (110)
[23/54] 6ter ... OK (800)
[24/54] RMC Story ... OK (0)
[25/54] RMC Découverte ... OK (0)
[26/54] RMC Life ... OK (0)
[27/54] 20 Minutes TV Île-de-France ... OK (830)
[28/54] BFM Business ... OK (1325)
[29/54] Guadeloupe La Première ... OK (1881)
[30/54] Guyane La Première ... OK (2707)
[31/54] Martinique La Première ... OK (1534)
[32/54] Mayotte La Première ... OK (1756)
[33/54] Nouvelle-Calédonie La Première ... OK (2035)
[34/54] Polynésie La Première ... OK (2007)
[35/54] Réunion La Première ... OK (2716)
[36/54] Saint-Pierre et Miquelon La Première ... OK (1208)
[37/54] Wallis-et-Futuna La Première ... OK (800)
[38/54] TV5 Monde ... OK (0)
[39/54] Equidia ...  OK (3043)
[40/54] beIN SPORTS ... erreur (134) (ancien cache conservé)
[41/54] La Une ... erreur (134) (ancien cache conservé)
[42/54] Tipik ... erreur (134) (ancien cache conservé)
[43/54] La Trois ... erreur (134) (ancien cache conservé)
[44/54] RTS 1 ... OK (20)
[45/54] RTS 2 ... OK (20)
[46/54] RTL-TVI ...  erreur (134) (ancien cache conservé)
[47/54] Club RTL ... erreur (134) (ancien cache conservé)
[48/54] Plug RTL ... erreur (134) (ancien cache conservé)
[49/54] ICI Tou.tv ... erreur (134) (ancien cache conservé)
[50/54] France 24 Français ... OK (503)
[51/54] France 24 Anglais ... OK (504)
[52/54] France 24 Arabe ... OK (486)
[53/54] France 24 Espagnol ... OK (504)
[54/54] La Télé ... erreur (134) (ancien cache conservé)

Catalogue: 94061 émission(s)

Cache écrit: /home/gg/captvty-cli/captvty-cache.json
```
Le cache est enregistré dans :

```text
captvty-cache.json
```

Lorsqu'une chaîne rencontre un timeout ou une erreur pendant `update`, son ancien contenu de cache est conservé.

## la structure du fichier captvty-cli.json

update construit le catalogue local. 

Pour chaque programme, il conserve uniquement channel, title et subtitle. 

Il met également à jour la date updated et l'état de chaque chaîne (ok, empty, timeout, error).

info n'enrichit pas le catalogue JSON. 

Elle interroge Captvty en temps réel et récupère des informations détaillées sur chaque émission et ses flux : 

numéro, titre, sous-titre, erreur éventuelle, résolution (width × height), débit (bitrate), flux préféré par Captvty (best) ainsi que les indicateurs internes LO, zzA, pk, au et uhA.

list, search et get ne modifient pas non plus le catalogue.

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

La consultation et la recherche des émissions TF1 peuvent fonctionner sans identifiants.

Pour le téléchargement des émissions du groupe TF1, un compte TF1 est nécessaire. Les identifiants sont fournis à la CLI par les variables d'environnement :

```bash
export TF1_USER="votre_utilisateur"
export TF1_PASS="votre_mot_de_passe"
```

La CLI utilise :

- `TF1_USER` : identifiant du compte TF1 ;
- `TF1_PASS` : mot de passe du compte TF1.

Il est préférable de ne pas inscrire ces identifiants directement dans le code source ni dans un fichier versionné sur GitHub.

Exemple :

```bash
export TF1_USER="mon_identifiant"
export TF1_PASS="mon_mot_de_passe"
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
si une série a 10 épisodes, le téléchargement de l'épisode 1 va échouer "2 vidéos correspondent"

la 1 et la 10 contiennent épisode 1

dans ce cas, indiquer une partie du texte de l'épisode 1


Pendant le téléchargement, la CLI peut afficher l'état Captvty, le média sélectionné et le chemin du fichier final.

## Installation et fichiers utilisés

Placez les fichiers compilés dans le répertoire contenant l'installation de Captvty.

Le répertoire d'exécution contient donc notamment :

```text
Captvty.exe
Captvty-cli-engine.exe
captvty-provider.exe
captvty-cli.exe
captvty-cache.json       # créé par update
bin/
```

Les autres fichiers et DLL nécessaires à Captvty doivent naturellement rester présents dans son installation.

`captvty-cache.json` est créé automatiquement lors d'une mise à jour.

## Interroger le cache avec jq

Le fichier `captvty-cache.json` peut aussi être consulté directement avec `jq`.

Afficher la date de dernière mise à jour :

```bash
jq -r '.updated' captvty-cache.json
2026-09-11T09:42:26.2259220+02:00
```

Afficher le nombre total d'émissions présentes dans le cache :

```bash
jq '.programs | length' captvty-cache.json
57814
```

Afficher la liste des chaînes présentes dans le cache :

```bash
jq -r '.programs[].channel' captvty-cache.json | sort -fu
Arte
France 2
France 3
France 4
France 5
LCP Assemblée Nationale
M6
Public Sénat
TF1
W9
```

Compter les émissions par chaîne :

```bash
jq -r '.programs[].channel' captvty-cache.json | sort | uniq -c | sort -nr
  15416 TF1
  14246 Arte
  10092 France 3
   5563 M6
   3303 France 2
   2734 France 4
   2311 W9
   1893 France 5
   1384 LCP Assemblée Nationale
    872 Public Sénat
```

Afficher les titres et sous-titres d'une chaîne, par exemple TF1 :

```bash
jq -r '.programs[]
  | select(.channel == "TF1")
  | [.title, .subtitle]
  | @tsv' captvty-cache.json
```

Rechercher un texte dans les titres ou sous-titres, sans tenir compte de la casse :

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

<PRE>
jq -r '.status | to_entries[] | [.key, .value] | @tsv' captvty-cache.json |   column -t -s $'\t'
<STRONG>
Arte                      ok
France 2                  ok
France 3                  ok
France 4                  ok
France 5                  ok
LCP Assemblée Nationale   ok
M6                        ok
Public Sénat              ok
TF1                       ok
W9                        ok
</STRONG>
</PRE>

Afficher uniquement les chaînes dont la dernière mise à jour n'est pas marquée `ok` :

<pre>
jq -r '.status
  | to_entries[]
  | select(.value != "ok")
  | [.key, .value]
  | @tsv' captvty-cache.json | column -t -s $'\t'
<strong>beIN SPORTS     empty
Club RTL        empty
CNews           empty
CStar           error
ICI Tou.tv      error
La Télé         empty
La Trois        empty
La Une          empty
NOVO19          empty
Plug RTL        empty
RMC Découverte  empty
RMC Life        empty
RMC Story       empty
RTL-TVI         empty
T18             empty
Tipik           empty
TV5 Monde       empty</strong>
</pre>

Rechercher une émission parmi tous les replays, par exemple looming tower
(la commande 

mono captvty-cli.exe search "looming tower"

sera disponible dans la version 2.4.2)

<pre>
jq -r ' .programs[] | select( ((.title // "") + " " + (.subtitle // "")) | test("looming tower"; "i") ) | [.channel, .title, .subtitle] | @tsv ' captvty-cache.json | column -t -s $'\t'
<strong>
M6  The looming tower : aux origines du 11 septembre - Episode 10 - 11 septembre
M6  The looming tower : aux origines du 11 septembre - Episode 6 - Des garçons va-t-en-guerre
M6  The looming tower : aux origines du 11 septembre - Episode 3 - Des erreurs ont été commises
M6  The looming tower : aux origines du 11 septembre - Episode 1 - Ça commence
M6  The looming tower : aux origines du 11 septembre - Episode 4 - Mercure
M6  The looming tower : aux origines du 11 septembre - Episode 7 - Le général
M6  The looming tower : aux origines du 11 septembre - Episode 8 - Une relation très particulière
M6  The looming tower : aux origines du 11 septembre - Episode 5 - Le bug de l'an 2000
M6  The looming tower : aux origines du 11 septembre - Episode 2 - Ma religion m'échappe
M6  The looming tower : aux origines du 11 septembre - Episode 9 - Mardi
</strong>  
</pre>

Vérifier si un replay utilise des DRM

Pour les chaînes utilisant le backend M6+, un média peut être correctement identifié et téléchargé par Captvty tout en restant protégé par DRM.

Vérification à partir des métadonnées

Avec le moteur instrumenté Captvty-cli-engine-m6-dump.exe, les métadonnées du replay sont enregistrées dans /tmp/m6.json.

Pour afficher les types de flux disponibles et savoir si leur URL indique une protection DRM :

<pre>jq -r '
  .clips[0].assets[]?
  | [
      (.video_container // ""),
      (.video_quality // ""),
      ((.full_physical_path // "") | test("drm"; "i"))
    ]
  | @tsv
' /tmp/m6.json | sort -u
<strong>
m3u8    hd    true
m3u8    sd    true
mpd     hd    true
mpd     sd    true
</strong>
</pre>

true indique ici que l'URL du média contient drm.

Pour afficher également les URL complètes :
<pre>
jq -r '
  .clips[0].assets[]?
  | [
      (.video_container // ""),
      (.video_quality // ""),
      (.full_physical_path // "")
    ]
  | @tsv
' /tmp/m6.json
<strong>
_drm_software.m3u8
_drm_software.mpd
</strong>
</pre>

## Un peu de documentation sur le fichier catvty-cache.json

Clés de premier niveau
<pre>
jq 'keys' captvty-cache.json
<strong>[
  "programs",
  "status",
  "updated",
  "version"
]
</strong>
</pre>

pour avoir un aperçu compact de toute la structure, sans les valeurs :
<pre>
jq 'to_entries[] | "\(.key): \(.value | type)"' captvty-cache.json
<strong>"version: number"
"updated: string"
"programs: array"
"status: object"
</strong>
</pre>
  
Pour voir la structure d'une émission sans afficher les milliers d'entrées :
<pre>
jq '.programs[0]' captvty-cache.json
<strong>{
  "channel": "TF1",
  "title": "Clem - S07 E04 - Nous nous sommes tant aimés (Partie 2)",
  "subtitle": ""
}
</strong>
</pre>

uniquement ses clés :
<pre>
<strong>
jq '.programs[0] | keys' captvty-cache.json
[
  "channel",
  "subtitle",
  "title"
]
</strong>
</pre>

uniquement ses clés
<pre>
jq '.status' captvty-cache.json
<strong>{
  "20 Minutes TV Île-de-France": "empty",
  "6ter": "empty",
  "Arte": "empty",
  "beIN SPORTS": "empty",
  "BFM Business": "empty",
  "BFMTV": "empty",
  "Club RTL": "empty",
  "CNews": "empty",
  "CStar": "error",
  "Equidia": "empty",
  "France 2": "empty",
  "France 24 Anglais": "empty",
  "France 24 Arabe": "empty",
  "France 24 Espagnol": "empty",
  "France 24 Français": "empty",
  "France 3": "empty",
  "France 4": "empty",
  "France 5": "empty",
  "Franceinfo": "empty",
  "Guadeloupe La Première": "empty",
  "Gulli": "empty",
  "Guyane La Première": "empty",
  "ICI Tou.tv": "error",
  "La chaîne L'Équipe": "empty",
  "La Télé": "empty",
  "La Trois": "empty",
  "La Une": "empty",
  "LCI": "empty",
  "LCP Assemblée Nationale": "empty",
  "M6": "empty",
  "Martinique La Première": "empty",
  "Mayotte La Première": "empty",
  "Nouvelle-Calédonie La Première": "empty",
  "NOVO19": "empty",
  "Plug RTL": "empty",
  "Polynésie La Première": "empty",
  "Public Sénat": "empty",
  "Réunion La Première": "empty",
  "RMC Découverte": "empty",
  "RMC Life": "empty",
  "RMC Story": "empty",
  "RTL-TVI": "empty",
  "RTS 1": "empty",
  "RTS 2": "empty",
  "Saint-Pierre et Miquelon La Première": "empty",
  "T18": "empty",
  "TF1": "error",
  "TF1 Séries Films": "error",
  "TFX": "error",
  "Tipik": "empty",
  "TMC": "error",
  "TV5 Monde": "empty",
  "W9": "empty",
  "Wallis-et-Futuna La Première": "empty"
}
</strong>
</pre>

pour avoir un aperçu compact de toute la structure, sans les valeurs :
<pre>
jq '
  def schema:
    if type == "object" then
      with_entries(.value |= schema)
    elif type == "array" then
      if length > 0 then [.[0] | schema] else [] end
    else
      type
    end;
  schema
' captvty-cache.json
<strong>{
  "version": "number",
  "updated": "string",
  "programs": [
    {
      "channel": "string",
      "title": "string",
      "subtitle": "string"
    }
  ],
  "status": {
    "20 Minutes TV Île-de-France": "string",
    "6ter": "string",
    "Arte": "string",
    "beIN SPORTS": "string",
    "BFM Business": "string",
    "BFMTV": "string",
    "Club RTL": "string",
    "CNews": "string",
    "CStar": "string",
    "Equidia": "string",
    "France 2": "string",
    "France 24 Anglais": "string",
    "France 24 Arabe": "string",
    "France 24 Espagnol": "string",
    "France 24 Français": "string",
    "France 3": "string",
    "France 4": "string",
    "France 5": "string",
    "Franceinfo": "string",
    "Guadeloupe La Première": "string",
    "Gulli": "string",
    "Guyane La Première": "string",
    "ICI Tou.tv": "string",
    "La chaîne L'Équipe": "string",
    "La Télé": "string",
    "La Trois": "string",
    "La Une": "string",
    "LCI": "string",
    "LCP Assemblée Nationale": "string",
    "M6": "string",
    "Martinique La Première": "string",
    "Mayotte La Première": "string",
    "Nouvelle-Calédonie La Première": "string",
    "NOVO19": "string",
    "Plug RTL": "string",
    "Polynésie La Première": "string",
    "Public Sénat": "string",
    "Réunion La Première": "string",
    "RMC Découverte": "string",
    "RMC Life": "string",
    "RMC Story": "string",
    "RTL-TVI": "string",
    "RTS 1": "string",
    "RTS 2": "string",
    "Saint-Pierre et Miquelon La Première": "string",
    "T18": "string",
    "TF1": "string",
    "TF1 Séries Films": "string",
    "TFX": "string",
    "Tipik": "string",
    "TMC": "string",
    "TV5 Monde": "string",
    "W9": "string",
    "Wallis-et-Futuna La Première": "string"
  }
}
</strong>
</pre>

## Vérification d'un fichier .ts téléchargé

Un téléchargement terminé avec succès ne signifie pas nécessairement que la vidéo est déchiffrée et lisible.

On peut examiner les flux avec ffprobe :
<pre>
ffprobe -v error \
  -show_entries stream=index,codec_name,codec_tag_string,codec_tag \
  -of compact \
  "Vidéos/fichier.ts"
</pre>
Sur les flux HLS protégés observés lors des tests M6+, le flux vidéo utilise le type MPEG-TS 0xDB, correspondant à HLS Sample Encryption.

Ainsi, un fichier .ts peut avoir une structure MPEG-TS valide, contenir des pistes vidéo et audio, et avoir été entièrement téléchargé, tout en conservant une vidéo protégée.

Résultats observés

Au cours des tests effectués avec Captvty 3.0.1.26, des replays de M6, W9 et 6ter ont fourni exclusivement des variantes DRM dans les métadonnées examinées, en HLS (m3u8) comme en DASH (mpd), en SD comme en HD.

Ces observations décrivent les replays testés et peuvent évoluer si les plateformes modifient leurs systèmes de diffusion.

Savoir si une vidéo est avec DRM 





## Codes de retour

Les principales valeurs utilisées sont :

- `0` : succès ;
- `1` : timeout ou échec d'une opération ;
- `2` : erreur d'utilisation, commande inconnue ou fichier requis absent ;
- `3` : cache absent ou vide pour une recherche locale.

Certains codes retournés par le worker Captvty peuvent aussi être propagés directement.

## Version 2.5.0

Principales évolutions :

- téléchargement `get` fonctionnel sous Mono avec les shims CefSharp ;
- ajout des sources `CefSharp-stub.cs`, `CefSharp-Core-stub.cs` et `CefSharp-WinForms-stub.cs` ;
- correction de la course concurrente rencontrée avec le provider BFMTV via `patch-bfmtv-hashset.cs` ;
- conservation de l ancien cache lorsqu un provider termine normalement mais retourne une liste vide ;
- amélioration de la recherche sur les combinaisons titre / sous-titre / épisode ;
- normalisation du nom du fichier après téléchargement ;
- qualité de téléchargement actuellement prise en charge : `high`.

## Version 2.4.1

Principales évolutions :

- ajout de l'option globale `--timeout` ;
- `--timeout 0` pour une attente illimitée ;
- timeout général porté à **120 secondes** ;
- conservation de timeouts plus longs pour `info` ciblé et `get` ;
- mise à jour du cache robuste : en cas de timeout ou d'erreur sur une chaîne, l'ancien cache est conservé ;
- compatibilité avec les sorties worker V1 et V2 ;
- ajout des sources permettant de reconstruire `Captvty-cli-engine.exe` depuis le `Captvty.exe` original ;
- ajout du patch de compatibilité Mono consolidé.

## SHA-256

Source `captvty-cli-v2.5.0.cs` :

```text
sha256sum captvty-cli-v2.5.0.cs
596b862f0351b442156c57d0139a1528a280f45b22c1c4bea857a99f77d41f46  captvty-cli-v2.5.0.cs
```

## Remarque

Ce projet repose sur l'observation et l'appel d'éléments internes de Captvty.

Les noms internes pouvant changer entre versions de Captvty, une nouvelle version de Captvty peut nécessiter une adaptation du moteur, des patches ou du worker.

Le dépôt ne fournit pas les binaires de Captvty lui-même.
