# Captvty CLI sous Linux / Mono

Interface en ligne de commande pour **Captvty 3.x**, destinée à piloter certaines fonctions de Captvty depuis **Mono sous Linux**.

## Version 2.9.0

La version **2.9.0** adapte Captvty CLI à **Captvty 3.0.1.28**
et a été validée sous **Debian 13** avec Mono.

Elle conserve les fonctionnalités T18 et TV5MONDE+
des versions précédentes.

Principales nouveautés :

- adaptation du provider à Captvty 3.0.1.28 ;
- simplification de la construction du moteur Mono ;
- deux patches Cecil modifiant seulement trois méthodes IL ;
- utilisation des DLL CefSharp originales pour les opérations validées ;
- adaptation à la nouvelle API du catalogue T18 ;
- mise à jour de yt-dlp et curl_cffi ;
- reprise des campagnes interrompues avec `update --resume` ;
- sauvegarde progressive du cache après chaque chaîne ;
- recherche ciblée avec `search --channel` ;
- regroupement des épisodes par titre lors des recherches ;
- conservation des identifiants de saison et d'épisode ;
- tri numérique des saisons et épisodes ;
- recherche par identifiant SxxExx dans le TUI ;
- nouveau fichier de cache `captvty-cache-28.json` ;
- 28 tests CLI et 8 tests TUI validés.

Les tests réseau T18 sélectionnent dynamiquement une émission
du catalogue, sans dépendre d'un titre d'épisode prédéfini.

Les principales validations concernent France Télévisions,
T18 ainsi que le catalogue et les sous-titres TV5MONDE+.

Toutes les chaînes n'ont pas fait l'objet d'une validation exhaustive.

## Version 2.8.0

La version **2.8.0** ajoute le support de **TV5MONDE+**.

Principales nouveautés :

- consultation du catalogue des séries et épisodes TV5MONDE+ ;
- affichage des langues de sous-titres disponibles pour un épisode ;
- téléchargement direct des sous-titres WebVTT ;
- téléchargement d'une ou plusieurs langues ;
- téléchargement de toutes les langues disponibles avec `all` ;
- téléchargement du français par défaut lorsqu'aucune langue n'est indiquée.

Les vidéos TV5MONDE+ étant protégées par DRM, cette prise en charge concerne le catalogue et les sous-titres, et non le téléchargement des vidéos.

## Version 2.7.2

La version **2.7.2** améliore la commande `info` :

- refus d'une recherche vide afin d'éviter une interrogation involontaire de l'ensemble du catalogue ;
- refus d'une recherche vide ciblée sur une chaîne, notamment `info "T18" ""` ;
- indication explicite qu'une commande comme `info T18` recherche l'émission « T18 » dans toutes les chaînes TV.

## Version 2.7.1

La version **2.7.1** améliore l'installation :

- vérification SHA256 de `Captvty.exe` avant toute compilation ;
- vérification que le binaire est bien le **Captvty 3.0.1.26 original** attendu ;
- message explicite en cas de version incompatible, notamment Captvty 3.0.1.27 ;
- correction de l'installation lorsque le répertoire source de Captvty CLI est aussi le répertoire de destination.

## Version 2.7.0

La version **2.7.0** ajoute notamment le support des replays de **T18**.

Principales nouveautés :

- récupération du catalogue des replays T18 ;
- téléchargement des émissions T18 ;
- utilisation de `yt-dlp` pour les flux T18 ;
- création automatique par `install.sh` d'un environnement Python dédié à `yt-dlp` ;
- résolution automatique des assemblies locales : `MONO_PATH` n'est plus nécessaire.

Le support T18 a été validé sous Linux avec Captvty 3.0.1.26.

Le projet fournit notamment :

- recherche d'émissions ;
- consultation détaillée des médias disponibles ;
- liste des émissions d'une chaîne ;
- téléchargement d'une émission ;
- cache local des programmes ;
- mise à jour partielle ou complète du cache ;
- timeout configurable pour éviter qu'un provider Captvty ne reste bloqué.

## Prérequis

- Linux (validation effectuée sous Debian 13) ;
- Mono et le compilateur `mcs` ;
- Mono.Cecil ;
- Python 3 et son module venv ;
- curl ;
- ffmpeg et ffprobe ;
- git pour récupérer les sources ;
- jq (optionnel, pour interroger le cache JSON) ;
- une installation originale de **Captvty 3.0.1.28**.

Sous Debian :

~~~bash
sudo apt install git mono-devel libmono-cecil-private-cil \
    python3 python3-venv curl ffmpeg
~~~

Pour installer également jq :

~~~bash
sudo apt install jq
~~~

La construction a été validée avec Mono.Cecil 0.11.1.0
sous Debian 13.

## Installation rapide

Télécharger et extraire la version Windows originale de
**Captvty 3.0.1.28**.

Il est recommandé de partir d'un répertoire propre, sans anciens
moteurs modifiés ni anciens shims CefSharp.

Cloner le dépôt :

~~~bash
git clone https://github.com/k3ck3c/captvty-cli.git
cd captvty-cli
~~~

Puis lancer :

~~~bash
bash install.sh
~~~

Le répertoire de destination peut également être indiqué directement :

~~~bash
bash install.sh /chemin/vers/Captvty
~~~

Le script :

- vérifie les dépendances ;
- vérifie le SHA-256 du Captvty.exe original ;
- compile les deux outils de patch ;
- construit le moteur compatible Mono ;
- compile le provider et la CLI ;
- vérifie la version du binaire obtenu ;
- sauvegarde les éventuels fichiers CLI précédents ;
- installe la CLI et le TUI ;
- crée un environnement Python dédié aux replays T18.

L'environnement `.ytdlp-venv` contient notamment :

| Composant | Version |
|---|---|
| yt-dlp | 2026.10.26 |
| curl_cffi | 0.17.0 |

L'installateur vérifie également la disponibilité de
l'impersonation Chrome par curl_cffi.

La CLI utilise en priorité le yt-dlp de cet environnement,
sans nécessiter son activation manuelle.

La migration d'une ancienne installation contenant des shims
ou des moteurs modifiés n'a pas été validée.

## Construction depuis les sources

L'utilisation de `install.sh` est recommandée.

La procédure manuelle ci-dessous décrit la construction
des composants C# de Captvty CLI 2.9.0.

Le dépôt ne fournit pas Captvty.exe.

Pour cette procédure, les sources de Captvty CLI et les fichiers
de Captvty 3.0.1.28 original doivent être présents dans le
répertoire courant.

Conserver notamment :

~~~text
Captvty.exe
Captvty.exe.config
bin/
~~~

### 1. Vérifier le binaire original

~~~bash
sha256sum Captvty.exe
~~~

Empreinte SHA-256 attendue pour Captvty 3.0.1.28 :

~~~text
13e04a10367e2e7c7492104a81d110470d8def2cd965037d69d2d5d634f756a7
~~~

Ne pas utiliser un exécutable déjà modifié.

### 2. Déterminer le chemin de Mono.Cecil

~~~bash
CECIL=$(find /usr/lib/mono/gac/Mono.Cecil \
    -name Mono.Cecil.dll | sort -V | tail -1)

echo "$CECIL"
~~~

### 3. Compiler les outils de patch

~~~bash
for patch in patch-dpi-28 patch-28-progress
do
    mcs -r:"$CECIL" \
        -out:"$patch.exe" \
        "$patch.cs"
done
~~~

### 4. Construire le moteur compatible Mono

~~~text
Captvty.exe
    |
    | patch-dpi-28
    v
Captvty-cli-engine-28.exe
    |
    | patch-28-progress
    v
Captvty-cli-engine-28-progress.exe
~~~

~~~bash
mono patch-dpi-28.exe \
    Captvty.exe \
    Captvty-cli-engine-28.exe

mono patch-28-progress.exe \
    Captvty-cli-engine-28.exe \
    Captvty-cli-engine-28-progress.exe
~~~

Le premier patch neutralise un appel à Bitmap.SetResolution
dans la méthode `_RMb::_Srb`.

Le second protège les deux méthodes de progression :

- `_qh/_X4B::_qJB(System.Double)` ;
- `_qh/_X4B::_a0()`.

La comparaison IL entre le binaire original et le moteur final
a donné :

~~~text
SUMMARY different=3 same=8442 onlyA=0 onlyB=0
~~~

Seulement trois méthodes sur 8445 sont modifiées.

Le moteur utilisé par défaut est
`Captvty-cli-engine-28-progress.exe`.

### 5. Compiler le provider

~~~bash
mcs -r:System.Core -r:System.Web.Extensions \
    -out:captvty-provider-28.exe \
    captvty-provider-28.cs
~~~

### 6. Compiler la CLI

~~~bash
mcs -r:System.Core -r:System.Web.Extensions \
    -out:captvty-cli.exe \
    captvty-cli-v2.9.0.cs
~~~

Quatre avertissements CS0649 concernant les classes utilisées
pour la désérialisation JSON TV5MONDE+ peuvent apparaître.

### 7. Fichiers nécessaires à l'exécution

~~~text
Captvty.exe
Captvty.exe.config
Captvty-cli-engine-28-progress.exe
captvty-provider-28.exe
captvty-cli.exe
captvty-tui.py
bin/
.ytdlp-venv/
~~~

La CLI configure automatiquement MONO_PATH pour ses providers.

Les DLL CefSharp originales présentes dans `bin/cefsharp/`
ont suffi pour les opérations validées avec Captvty 3.0.1.28.

La compilation des anciens shims CefSharp n'est donc plus
effectuée par l'installateur 2.9.0.

Pour préparer également l'environnement Python dédié à T18,
utiliser `install.sh`.

### Validation de la construction

La procédure d'installation a été validée sous Debian 13
à partir d'une installation originale de Captvty 3.0.1.28.

~~~bash
mono captvty-cli.exe --version
~~~

Résultat attendu :

~~~text
captvty-cli 2.9.0
~~~

Exemple :

~~~bash
mono captvty-cli.exe list "France 3" "Saint-Pierre"
~~~

L'ancienne procédure de construction pour Captvty 3.0.1.26
est conservée dans
[docs/BUILD-3.0.1.26.md](docs/BUILD-3.0.1.26.md).

## Utilisation


Les principales commandes sont :

```bash
mono captvty-cli.exe --version

mono captvty-cli.exe [--timeout secondes] update [--all | --resume | "chaîne" ...]
mono captvty-cli.exe [--timeout secondes] search [--live] [--channel "chaîne"] "texte"
mono captvty-cli.exe [--timeout secondes] list "chaîne" "texte"
mono captvty-cli.exe [--timeout secondes] info "texte"
mono captvty-cli.exe [--timeout secondes] info "chaîne" "texte"
mono captvty-cli.exe [--timeout secondes] get "chaîne" "titre" "sous-titre" high|low|N

# TV5MONDE+
mono captvty-cli.exe [--timeout secondes] info "TV5 Monde" "titre" ["S1 E1"]
mono captvty-cli.exe [--timeout secondes] get "TV5 Monde" "titre" "S1 E1" [fr|fr,es,...|all]
mono captvty-cli.exe [--timeout secondes] tv5subs ASSET_ID [LANG] [OUTPUT.vtt]
```
### Diagnostic du provider

Le provider peut être interrogé directement pour diagnostiquer le
chargement d'une chaîne.

Compter les émissions chargées :

    MONO_PATH="$PWD:$PWD/bin:$PWD/bin/cefsharp" mono captvty-provider-28.exe catalog \
      Captvty-cli-engine-28-progress.exe "France 2"

Afficher les chaînes connues du moteur :

    MONO_PATH="$PWD:$PWD/bin:$PWD/bin/cefsharp" mono captvty-provider-28.exe channels \
      Captvty-cli-engine-28-progress.exe

Cette commande émet une ligne CHANNEL par chaîne.

Afficher le catalogue brut utilisé par `update` :

    MONO_PATH="$PWD:$PWD/bin:$PWD/bin/cefsharp" mono captvty-provider-28.exe dump \
      Captvty-cli-engine-28-progress.exe "France 2" ""

La commande `dump` émet une ligne `ITEM` par émission.

Les champs sont séparés par des tabulations :

| Champ | Contenu |
|---|---|
| 1 | ITEM |
| 2 | Chaîne |
| 3 | Numéro original de l'émission |
| 4 | Titre |
| 5 | Sous-titre |
| 6 | Valeur brute episode_id du provider |

Les traitements autonomes T18 et TV5MONDE+ ne nécessitent
pas ce provider.

Ces commandes sont principalement destinées au diagnostic et ne font
pas partie de l'interface utilisateur normale de `captvty-cli`.

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

| Commande | Timeout |
|---|---:|
| update | 120 secondes |
| search --live | 120 secondes |
| list | 120 secondes |
| info sur toutes les chaînes | 120 secondes |
| info ciblé sur une chaîne | 600 secondes |
| get classique | 12 heures + 60 secondes |
| get T18 | 12 heures + 60 secondes |
| get TV5MONDE+ | 600 secondes |
| tv5subs | 120 secondes |

Pour les opérations multichaînes utilisant le provider,
le timeout s'applique à chaque interrogation et non à la
durée totale de la commande.

Pour T18 et TV5MONDE+, plusieurs requêtes successives
peuvent utiliser chacune le timeout indiqué.

Une valeur fournie explicitement avec `--timeout`
remplace le timeout par défaut de la commande.

La valeur 0 désactive le timeout imposé par la CLI,
mais pas nécessairement les limites internes des composants
ou des services distants.

## Mise à jour du cache

Le catalogue local est enregistré dans le fichier :

~~~text
captvty-cache-28.json
~~~

Ce fichier est distinct du cache des versions précédentes.

### Mise à jour des premières chaînes

Sans argument, update traite les 10 premières chaînes
du catalogue Captvty.

~~~bash
mono captvty-cli.exe update
~~~

### Mise à jour de chaînes précises

~~~bash
mono captvty-cli.exe update "France 2" "France 3"
~~~

Exemple de résultat obtenu avec la version 2.9.0 :

~~~text
[1/2] France 2 ... OK (8228)
[2/2] France 3 ... OK (13627)
~~~

Les nombres d'émissions varient selon la date et les catalogues
disponibles.

T18 peut également être actualisée indépendamment :

~~~bash
mono captvty-cli.exe update T18
~~~

### Mise à jour complète

~~~bash
mono captvty-cli.exe update --all
~~~

La liste des chaînes est récupérée auprès du moteur Captvty.

Depuis la version 2.9.0, le cache est sauvegardé progressivement
après le traitement de chaque chaîne.

En cas d'erreur, de timeout ou de catalogue vide, les anciennes
émissions de la chaîne sont conservées.

### Interruption et reprise

Une campagne complète peut être interrompue avec Ctrl+C.

Pour reprendre la campagne :

~~~bash
mono captvty-cli.exe update --resume
~~~

Les chaînes déjà traitées avec succès ne sont pas interrogées
une nouvelle fois.

Les chaînes restantes, notamment celles ayant rencontré une
erreur, peuvent être traitées lors de la reprise.

L'état de la campagne est conservé dans la section
`update_session` du fichier JSON.

La reprise concerne les campagnes lancées avec `update --all`.

En l'absence de campagne interrompue à reprendre, la commande
retourne le code 2.

### Validation de la reprise

Un premier test a été effectué avec une campagne simulée
comprenant France 2, France 3 et T18.

France 2 et France 3 étaient déjà marquées comme terminées.

La reprise a traité uniquement T18 :

~~~text
[1/1] T18 ... OK (207)
~~~

Les catalogues France 2 et France 3 sont restés inchangés.
La campagne a ensuite été marquée comme terminée.

Un second test a été effectué avec une véritable campagne
de 54 chaînes :

~~~text
[1/54] TF1 ... OK (14599)
[2/54] France 2 ... ^C
~~~

Après interruption, le cache contenait les 14599 émissions
de TF1.

La commande update --resume a repris à France 2 :

~~~text
[1/53] France 2 ... OK (8390)
[2/53] France 3 ... ^C
~~~

Après cette seconde interruption :

- TF1 : 14599 émissions ;
- France 2 : 8390 émissions ;
- total : 22989 émissions ;
- 2 chaînes terminées ;
- 52 chaînes restant à traiter.

La date initiale de la campagne a été conservée.

Ces tests valident la sauvegarde progressive et la reprise
après interruption, mais pas le fonctionnement exhaustif
des 54 chaînes.

Les anciens résultats de mise à jour sont conservés dans
[docs/UPDATE-HISTORIQUE.md](docs/UPDATE-HISTORIQUE.md).

## Structure du fichier captvty-cache-28.json

Le cache utilise le format JSON version 2.

### Structure générale

| Champ | Description |
|---|---|
| version | Version du format JSON |
| updated | Date de dernière sauvegarde |
| programs | Tableau des émissions |
| status | État de la dernière mise à jour de chaque chaîne |
| update_session | État de la campagne complète, si présente |

### Structure d'une émission

| Champ | Description |
|---|---|
| channel | Nom de la chaîne |
| title | Titre de l'émission |
| subtitle | Sous-titre de l'émission |
| episode_id | Identifiant de saison et d'épisode |

Exemple réel extrait du cache 2.9.0 :

~~~json
{
  "channel": "TF1",
  "title": "Plus belle la vie - S13 E01",
  "subtitle": "",
  "episode_id": ""
}
~~~

Le champ episode_id conserve la valeur fournie par le provider.

Il peut contenir :

- un identifiant de saison et d'épisode ;
- un identifiant suivi du sous-titre ;
- le sous-titre seul ;
- une chaîne vide.

Exemple réel :

~~~json
{
  "channel": "France 2",
  "title": "Capitaine Marleau",
  "subtitle": "Pace e salute, Marleau !",
  "episode_id": "S03E04 Pace e salute, Marleau !"
}
~~~

Le champ episode_id n'est donc pas un identifiant unique
ni une valeur systématiquement normalisée.

La CLI et le TUI extraient les numéros de saison et d'épisode
pour effectuer un tri numérique lorsqu'ils sont reconnus.

La présence d'un numéro de saison ou d'épisode dans le titre
ne garantit pas que ce champ soit renseigné.

### État des chaînes

Les valeurs possibles de status sont :

| Valeur | Signification |
|---|---|
| ok | Catalogue récupéré avec succès |
| empty | Catalogue vide |
| timeout | Délai maximal dépassé |
| error | Erreur lors de la récupération |

### État de la campagne de mise à jour

La section update_session permet de reprendre une campagne
lancée avec update --all.

| Champ | Description |
|---|---|
| started | Date initiale de la campagne |
| completed | Indique si la campagne est terminée |
| channels | Liste ordonnée des chaînes prévues |
| done | Liste des chaînes traitées avec succès |

La date started est conservée lors des reprises successives.

### Interroger l'état de la campagne avec jq

Afficher la campagne :

~~~bash
jq '.update_session' captvty-cache-28.json
~~~

Afficher les chaînes terminées :

~~~bash
jq -r '.update_session.done[]' captvty-cache-28.json
~~~

Afficher les chaînes restant à traiter :

~~~bash
jq -r '
    .update_session |
    (.channels - .done)[]
' captvty-cache-28.json
~~~

Afficher la prochaine chaîne :

~~~bash
jq -r '
    .update_session |
    (.channels - .done)[0] // "Aucune"
' captvty-cache-28.json
~~~

Afficher la progression :

~~~bash
jq '
    .update_session |
    {
        total: (.channels | length),
        terminees: (.done | length),
        restantes: ((.channels - .done) | length),
        completed
    }
' captvty-cache-28.json
~~~

### Sauvegarde du cache

Le cache est sauvegardé après le traitement de chaque chaîne.

La sauvegarde utilise un fichier temporaire avant de remplacer
le fichier JSON principal.

### Informations non conservées dans le cache

La commande info n'enrichit pas le catalogue JSON.

Elle interroge les services en temps réel pour récupérer
les informations détaillées sur les émissions et les médias.

Les résolutions, débits et informations sur les flux ne sont
pas conservés dans le cache.

Les commandes list, search, info et get ne modifient pas
le catalogue JSON.

## Recherche

### Recherche dans le cache

~~~bash
mono captvty-cli.exe search "Saint-Pierre"
~~~

Si le fichier captvty-cache-28.json existe,
il est utilisé par défaut.

Si le fichier n'existe pas, la CLI bascule automatiquement
vers une recherche directe.

Si le fichier existe mais que le catalogue lu est vide,
la recherche retourne le code 3.

La recherche dans le cache est insensible à la casse
et aux accents, grâce à la normalisation Unicode
des titres, des sous-titres et du texte recherché.

Les résultats sont regroupés par chaîne et par titre,
avec indication du nombre d'émissions correspondantes.

### Recherche ciblée sur une chaîne

Depuis la version 2.9.0, l'option --channel permet de
limiter la recherche à une chaîne précise.

~~~bash
mono captvty-cli.exe search --channel "France 3" "Saint-Pierre"

mono captvty-cli.exe search --channel "T18" "The Closer"
~~~

Le nom de la chaîne est comparé sans distinction
entre majuscules et minuscules.

### Recherche directe

L'option --live permet d'interroger directement les services,
sans utiliser le catalogue local.

Recherche sur toutes les chaînes :

~~~bash
mono captvty-cli.exe search --live "Saint-Pierre"
~~~

Recherche limitée à France 3 :

~~~bash
mono captvty-cli.exe search --live --channel "France 3" "Saint-Pierre"
~~~

Recherche directe sur T18 :

~~~bash
mono captvty-cli.exe search --live --channel "T18" "The Closer"
~~~

Dans ce dernier cas, la CLI utilise directement l'API T18,
sans passer par le moteur Captvty.

Les options --live et --channel doivent précéder
le texte recherché.

### Validation des recherches

Les tests réalisés avec la version 2.9.0 ont notamment donné :

| Recherche | Nombre d'émissions |
|---|---:|
| Saint-Pierre dans le cache | 219 |
| Saint-Pierre sur France 3 dans le cache | 219 |
| Saint-Pierre sur France 3 en direct | 219 |
| The Closer sur T18 dans le cache | 14 |
| The Closer sur T18 en direct | 14 |

La recherche Saint-Pierre a produit huit groupes de titres.

La recherche ciblée sur France 3 en direct a pris environ
une minute, contre environ une demi-seconde dans le cache.

La recherche directe sur T18 a pris environ neuf secondes.

Ces résultats dépendent de la disponibilité des catalogues
et des conditions réseau.

La recherche directe sur l'ensemble des chaînes n'a pas
été revalidée exhaustivement avec la version 2.9.0.

## Groupe TF1 : authentification

> **Attention : documentation historique.**
>
> Avec la version 2.9.0, la consultation du catalogue TF1
> a été validée, mais pas le téléchargement des vidéos.
> Les nouveaux sources CLI et provider ne lisent pas
> explicitement les variables TF1_USER et TF1_PASS.
> La procédure ci-dessous est conservée à titre historique
> et reste à revalider avec Captvty 3.0.1.28.

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

~~~bash
mono captvty-cli.exe list "TF1" "plus belle la vie"
~~~

Exemple historique de sortie :

~~~text
Résultats sur TF1 pour "plus belle la vie" :
  1. Plus belle la vie, encore plus belle ...
  2. Plus belle la vie ...

485 émission(s)
~~~

Pour les chaînes classiques, la commande list interroge
directement le catalogue de la chaîne.

Une recherche importante peut prendre près d'une minute
ou davantage selon la chaîne et le réseau.

Le timeout par défaut est de 120 secondes.

### Tri numérique des saisons et épisodes

Depuis la version 2.9.0, les résultats du provider sont
triés selon les identifiants de saison et d'épisode.

La reconnaissance utilise l'expression régulière :

~~~text
^S(\d+)E(\d+)\b
~~~

Cette expression est appliquée au champ episode_id,
sans distinction entre majuscules et minuscules.

Le tri respecte les critères suivants :

1. émissions avec un identifiant reconnu ;
2. numéro de saison croissant ;
3. numéro d'épisode croissant ;
4. ordre initial en cas d'égalité.

Les émissions sans identifiant reconnu sont placées
après les épisodes numérotés, dans leur ordre initial.

Le numéro affiché devant chaque émission reste celui
attribué par le provider. Il n'est pas recalculé après le tri.

### Exemple validé avec France 3

~~~bash
mono captvty-cli.exe list "France 3" "Saint-Pierre"
~~~

Lors du test effectué avec la version 2.9.0,
219 émissions ont été retournées.

Les dix premières entrées correspondaient aux épisodes
suivants :

| Numéro d'origine | Épisode |
|---:|---|
| 199 | S01E01 |
| 96 | S01E02 |
| 62 | S01E03 |
| 89 | S01E04 |
| 105 | S01E05 |
| 162 | S01E06 |
| 36 | S01E07 |
| 70 | S01E08 |
| 100 | S01E09 |
| 80 | S01E10 |

Cet exemple illustre la conservation des numéros
d'origine malgré le changement d'ordre d'affichage.

### Cas particulier de T18

~~~bash
mono captvty-cli.exe list "T18" "The Closer"
~~~

La commande list utilise le catalogue local pour T18.

Pour actualiser préalablement ce catalogue :

~~~bash
mono captvty-cli.exe update T18
~~~

Le test réalisé avec la version 2.9.0 a retourné
14 épisodes de The Closer.

### Cas particulier de TV5MONDE+

~~~bash
mono captvty-cli.exe list "TV5 Monde" "Philharmonia"
~~~

La CLI interroge directement l'API TV5MONDE+,
indépendamment du moteur Captvty.

Les six épisodes de Philharmonia ont été récupérés
et affichés dans l'ordre des épisodes.

Voir également la section consacrée à TV5MONDE+.

## Informations détaillées

La commande info interroge les services en temps réel,
indépendamment du contenu du cache local.

### Recherche sur toutes les chaînes

~~~bash
mono captvty-cli.exe info "plus belle la vie"
~~~

Le timeout par défaut est de 120 secondes par interrogation.

### Recherche ciblée sur une chaîne

~~~bash
mono captvty-cli.exe info "France 3" "Saint-Pierre"
~~~

Il est également possible de préciser le sous-titre :

~~~bash
mono captvty-cli.exe info "France 3" "Saint-Pierre - Le miel et les affaires"
~~~

Le timeout par défaut d'une recherche ciblée est de
600 secondes.

### Classement des médias

Pour les chaînes utilisant le moteur Captvty, les médias
sont classés selon les critères suivants :

1. média préféré par Captvty ;
2. nombre total de pixels décroissant (largeur x hauteur) ;
3. débit décroissant à résolution égale.

Les commandes info et get utilisent les mêmes critères
de classement.

L'astérisque et la mention [choix Captvty] signalent
le média préféré par le moteur Captvty.

La commande get avec une qualité numérique utilise
la position du média dans la liste triée, à partir de 1.

En cas d'égalité complète des critères ou de modification
des médias disponibles entre info et get, la correspondance
des positions n'est pas garantie.

### Estimation de la taille des fichiers

Lorsque le débit et la durée sont connus, la CLI affiche
une estimation de la taille du fichier en Mo ou Go.

Pour les médias utilisant le calcul PrintMedia :

~~~text
taille_Mo = (debit_media + debit_audio_supplementaire)
            * duree_secondes / 8000 * 1.04
~~~

Les débits sont exprimés en kb/s.

Le débit HLS comprend déjà une piste audio.
Les débits des pistes audio supplémentaires connues
sont ajoutés sans compter deux fois cette première piste.

Une marge de 4 % est ensuite appliquée.

Les tailles affichées sont indicatives et peuvent différer
de la taille réelle du fichier téléchargé.

### Exemple validé avec France 3

Emission : Saint-Pierre - Le miel et les affaires.

Quatre qualités ont été obtenues :

| Qualité | Débit (kb/s) |
|---|---:|
| 1080p | 3043 |
| 1080p | 1722 |
| 720p | 1013 |
| 576p | 621 |

Durée affichée : 43 min 41 s.

### Informations détaillées sur T18

~~~bash
mono captvty-cli.exe info "T18" "The Closer - Le Baiser de la mort"
~~~

La résolution des médias T18 utilise yt-dlp,
indépendamment du moteur Captvty.

Exemple obtenu avec la version 2.9.0 :

| Qualité | Débit (kb/s) | Taille estimée |
|---|---:|---:|
| 1080p | 6222 | 1.95 Go |
| 720p | 2149 | 673 Mo |
| 480p | 836 | 262 Mo |
| 288p | 461 | 144 Mo |

Durée affichée : 40 min 08 s.

### Informations détaillées sur TV5MONDE+

~~~bash
mono captvty-cli.exe info "TV5 Monde" "Philharmonia" "S1 E1"
~~~

Pour TV5MONDE+, la commande info fournit notamment
les identifiants AssetId et les langues de sous-titres
disponibles.

Lors de notre test, huit langues étaient proposées :

ar, de, en, es, fr, ln, ro, sw.

Cette fonctionnalité concerne le catalogue et les
sous-titres, et non le téléchargement des vidéos
protégées par DRM.

Voir la section TV5MONDE+ ci-dessous.

## TV5MONDE+

La version **2.8.0** ajoute le support du catalogue et des sous-titres de **TV5MONDE+**.

Les vidéos TV5MONDE+ sont protégées par DRM. Captvty CLI ne tente pas de télécharger ou de déchiffrer ces vidéos. Les sous-titres sont en revanche disponibles séparément sous forme de fichiers WebVTT.

### Rechercher une série

```bash
mono captvty-cli.exe list "TV5 Monde" "Castes"
```

Exemple :

```text
Castes, les amours interdites  [107584472_74079A]
  1. S1 E1 - le rituel du seuil  [107550310_74079A]
  2. S1 E2 - l annonce et les secrets  [107550311_74079A]
  ...
  60. S1 E60 - le dernier nom  [107550369_74079A]

1 série(s)
```

### Afficher les épisodes

```bash
mono captvty-cli.exe info "TV5 Monde" "Castes"
```

### Afficher les sous-titres disponibles

```bash
mono captvty-cli.exe info "TV5 Monde" "Castes" "S1 E1"
```

Exemple :

```text
Castes, les amours interdites
AssetId série : 107584472_74079A

S1 E1 - le rituel du seuil
AssetId : 107550310_74079A
Sous-titres : ln sw de es ar ro en fr
```

La liste des langues dépend du programme.

### Télécharger les sous-titres

Sans indication de langue, le français est téléchargé par défaut :

```bash
mono captvty-cli.exe get "TV5 Monde" "Castes" "S1 E1"
```

La CLI l'indique explicitement :

```text
Aucune langue indiquée : français (fr) téléchargé par défaut.
```

Pour demander explicitement le français :

```bash
mono captvty-cli.exe get "TV5 Monde" "Castes" "S1 E1" fr
```

Plusieurs langues peuvent être demandées en les séparant par des virgules :

```bash
mono captvty-cli.exe get "TV5 Monde" "Castes" "S1 E1" fr,es,ar
```

Pour télécharger toutes les langues disponibles :

```bash
mono captvty-cli.exe get "TV5 Monde" "Castes" "S1 E1" all
```

Exemple de fichiers créés :

```text
castes-les-amours-interdites-S01E01.fr.vtt
castes-les-amours-interdites-S01E01.es.vtt
castes-les-amours-interdites-S01E01.ar.vtt
```

Lors du test avec l'épisode S1 E1 de « Castes, les amours interdites », huit langues étaient disponibles : `ln`, `sw`, `de`, `es`, `ar`, `ro`, `en` et `fr`.

### Codes de langue

| Code | Langue |
|------|--------|
| `fr` | français |
| `en` | anglais |
| `es` | espagnol |
| `de` | allemand |
| `ro` | roumain |
| `ar` | arabe |
| `ln` | lingala |
| `sw` | swahili |

### Commande de diagnostic

Une commande de bas niveau permet également de télécharger directement un sous-titre à partir de l'identifiant TV5MONDE+ :

```bash
mono captvty-cli.exe tv5subs 107550310_74079A fra sortie.vtt
```

Cette commande est principalement destinée au diagnostic. L'utilisation normale avec `list`, `info` et `get` ne nécessite pas de connaître les identifiants TV5MONDE+.

### Fonctionnement

Le chemin utilisé est schématiquement :

```text
catalogue TV5MONDE+
        |
        v
série / épisode
        |
        v
AssetId
        |
        v
RedBee
        |
        v
sous-titres disponibles
        |
        v
fichiers WebVTT
```

Pour TV5MONDE+, `list`, `info` et `get` utilisent directement le catalogue TV5MONDE+ et RedBee pour ces opérations et ne dépendent pas du provider Captvty.

### Validation avec la version 2.9.0

Le fonctionnement autonome introduit dans la version 2.8.0
a été conservé lors de l'intégration de Captvty 3.0.1.28.

Les tests de non-régression ont été réalisés avec la série
Philharmonia, sans moteur ni provider Captvty.

#### Consultation du catalogue

~~~bash
mono captvty-cli.exe list "TV5 Monde" "Philharmonia"
~~~

Les six épisodes ont été retournés dans l'ordre.

AssetId de la série : 107555913_74079A.

#### Informations sur un épisode

~~~bash
mono captvty-cli.exe info "TV5 Monde" "Philharmonia" "S1 E1"
~~~

AssetId de l'épisode : 107549217_74079A.

Huit langues de sous-titres étaient disponibles.

#### Téléchargement direct par AssetId

~~~bash
mono captvty-cli.exe tv5subs 107549217_74079A fra phil-s01e01-fr.vtt
~~~

Le fichier WebVTT français obtenu contenait 578 blocs
de sous-titres.

#### Téléchargement par titre et épisode

Les trois commandes suivantes ont été validées :

~~~bash
mono captvty-cli.exe get "TV5 Monde" "Philharmonia" "S1 E1" fr

mono captvty-cli.exe get "TV5 Monde" "Philharmonia" "S1 E1" fr,es,ar

mono captvty-cli.exe get "TV5 Monde" "Philharmonia" "S1 E1" all
~~~

La dernière commande a produit les huit fichiers suivants :

| Fichier | Blocs de sous-titres |
|---|---:|
| philharmonia-S01E01.ar.vtt | 599 |
| philharmonia-S01E01.de.vtt | 597 |
| philharmonia-S01E01.en.vtt | 589 |
| philharmonia-S01E01.es.vtt | 588 |
| philharmonia-S01E01.fr.vtt | 578 |
| philharmonia-S01E01.ln.vtt | 922 |
| philharmonia-S01E01.ro.vtt | 609 |
| philharmonia-S01E01.sw.vtt | 922 |

#### Temps d'exécution observés

| Opération | Durée approximative |
|---|---:|
| list | 2.3 s |
| info sur un épisode | 5.7 s |
| tv5subs | 0.6 s |
| get fr | 2.4 s |
| get fr,es,ar | 3.5 s |
| get all | 3.5 s |

Ces durées dépendent des services distants et du réseau.

#### Limites de la validation

Les tests ont confirmé la récupération directe des fichiers
WebVTT et le fonctionnement indépendant du moteur Captvty.

Le mécanisme de secours WVTT et le téléchargement sans
indication explicite de langue n'ont pas été retestés
avec la version 2.9.0.

La disponibilité des séries et des langues peut évoluer.

## Téléchargement

### Syntaxe générale

~~~bash
mono captvty-cli.exe get "chaîne" "titre" "sous-titre" high
~~~

Pour les chaînes utilisant le moteur Captvty, trois modes
de sélection de la qualité sont disponibles :

| Qualité | Signification |
|---|---|
| high | Média préféré par Captvty |
| low | Dernier média de la liste triée |
| N | Position du média dans la liste triée, à partir de 1 |

Il est conseillé d'utiliser préalablement la commande info
pour connaître les médias disponibles.

### Exemple de téléchargement

~~~bash
mono captvty-cli.exe get \
  "France 3" \
  "Saint-Pierre" \
  "Le miel et les affaires" \
  high
~~~

### Sélection d'un épisode

La recherche par sous-titre peut retourner plusieurs
émissions lorsque le texte recherché est trop court.

Par exemple, une recherche sur "épisode 1" peut également
correspondre à "épisode 10".

Dans ce cas, préciser une partie distinctive du sous-titre
de l'épisode souhaité.

### Suivi du téléchargement

Pendant le téléchargement, la CLI peut afficher :

- le média sélectionné ;
- les états successifs du moteur Captvty ;
- le chemin du fichier final.

Le timeout par défaut est de 12 heures et 60 secondes.

### Validation avec Captvty 3.0.1.28

Un téléchargement de Saint-Pierre, épisode
Le miel et les affaires, a été effectué avec la version 2.9.0.

Le fichier obtenu a été analysé avec ffprobe.

| Caractéristique | Valeur |
|---|---|
| Conteneur | MPEG-TS |
| Codec vidéo | H.264 |
| Résolution | 1920 x 1080 |
| Codec audio | AAC |
| Pistes audio | 3 (fr, qaa, qad) |
| Durée | 2622.004 secondes |
| Taille | 1119680436 octets |
| Débit global | 3416258 bits/s |
| Temps de téléchargement | environ 2 min 37 s |

### T18

La version 2.7.0 a introduit le support des replays T18.

Lors des tests historiques de cette version,
166 replays avaient été récupérés.

La version 2.9.0 adapte la récupération du catalogue
aux modifications du site et de l'API T18.

La documentation technique de cette adaptation est disponible
dans [docs/T18-API.md](docs/T18-API.md).

#### Mise à jour du catalogue

~~~bash
mono captvty-cli.exe update T18
~~~

Lors des tests de la version 2.9.0, cette commande
a récupéré 207 replays.

Les catalogues des autres chaînes ont été conservés.

#### Recherche et consultation

~~~bash
mono captvty-cli.exe search --channel "T18" "The Closer"

mono captvty-cli.exe search --live --channel "T18" "The Closer"

mono captvty-cli.exe list "T18" "The Closer"

mono captvty-cli.exe info "T18" "The Closer - Le Baiser de la mort"
~~~

#### Téléchargement

~~~bash
mono captvty-cli.exe get "T18" "The Closer" "Le Baiser de la mort" high
~~~

Pour T18, la CLI utilise yt-dlp indépendamment
du moteur Captvty.

L'environnement Python nécessaire est préparé
automatiquement par install.sh.

La CLI recherche en priorité yt-dlp dans le répertoire
local .ytdlp-venv/bin, puis dans le PATH.

Versions validées avec la 2.9.0 :

| Composant | Version |
|---|---|
| yt-dlp | 2026.10.26 |
| curl_cffi | 0.17.0 |

ffmpeg et ffprobe doivent également être disponibles.

#### Validation du téléchargement T18

Le téléchargement de l'épisode Le Baiser de la mort
de la série The Closer a été effectué avec succès.

Le fichier obtenu a été analysé avec ffprobe.

| Caractéristique | Valeur |
|---|---|
| Conteneur | MP4 |
| Codec vidéo | H.264 |
| Résolution | 1920 x 1080 |
| Codec audio | AAC |
| Pistes audio | 1 |
| Durée | 2407.887 secondes |
| Taille | 1432829050 octets |
| Débit global | 4760452 bits/s |
| Temps de téléchargement | environ 3 min 59 s |

#### Limites de la validation

Les fichiers France 3 et T18 ont été contrôlés avec ffprobe,
mais aucun décodage intégral n'a été effectué.

Ces tests ne constituent pas une validation du téléchargement
sur l'ensemble des chaînes et des qualités disponibles.

### TV5MONDE+

Pour TV5MONDE+, la commande get est réservée
au téléchargement des sous-titres.

Le dernier argument désigne alors une langue,
une liste de langues ou all, et non une qualité vidéo.

Voir la section TV5MONDE+.

## Installation et fichiers utilisés

L'installation de Captvty CLI 2.9.0 repose sur une
distribution originale de Captvty 3.0.1.28.

Le script install.sh compile et installe les composants
nécessaires dans le répertoire de Captvty.

### Répertoire d'exécution

~~~text
Captvty.exe
Captvty.exe.config
Captvty-cli-engine-28-progress.exe
captvty-provider-28.exe
captvty-cli.exe
captvty-tui.py
captvty-cache-28.json
.ytdlp-venv/
bin/
    cefsharp/
~~~

### Rôle des fichiers

| Fichier | Rôle |
|---|---|
| Captvty.exe | Exécutable original non modifié |
| Captvty.exe.config | Configuration originale |
| Captvty-cli-engine-28-progress.exe | Moteur adapté à Mono |
| captvty-provider-28.exe | Interface avec le moteur |
| captvty-cli.exe | Interface en ligne de commande |
| captvty-tui.py | Interface interactive en mode texte |
| captvty-cache-28.json | Catalogue local et état des campagnes |
| .ytdlp-venv/ | Environnement Python utilisé pour T18 |
| bin/ | Dépendances originales de Captvty |
| bin/cefsharp/ | Bibliothèques CefSharp originales |

Le fichier captvty-cache-28.json est créé automatiquement
lors d'une mise à jour du catalogue.

### Conservation des fichiers originaux

Le fichier Captvty.exe original n'est pas modifié.

Les adaptations nécessaires à Mono sont appliquées
à une copie distincte du moteur.

Les bibliothèques originales de Captvty doivent rester
présentes dans le répertoire d'installation.

La version 2.9.0 utilise les bibliothèques CefSharp
originales fournies avec Captvty 3.0.1.28.

### Environnement Mono

La CLI configure automatiquement les chemins nécessaires
à la recherche des assemblies Mono.

Il n'est normalement pas nécessaire de définir MONO_PATH
avant d'utiliser captvty-cli.exe.

### Compatibilité avec les anciennes installations

Une installation propre à partir de Captvty 3.0.1.28
a été validée sous Debian 13.

La migration directe d'une installation antérieure
n'a pas été testée.

Le nouveau cache porte un nom distinct de celui utilisé
par les anciennes versions.

## Interroger le cache avec jq

Le fichier captvty-cache-28.json peut être consulté
directement avec jq.

### Date de dernière mise à jour

~~~bash
jq -r '.updated' captvty-cache-28.json
~~~

### Nombre total d'émissions

~~~bash
jq '.programs | length' captvty-cache-28.json
~~~

### Liste des chaînes présentes dans le cache

~~~bash
jq -r '.programs[].channel' captvty-cache-28.json | sort -fu
~~~

### Nombre d'émissions par chaîne

~~~bash
jq -r '.programs[].channel' captvty-cache-28.json |
    sort | uniq -c | sort -nr
~~~

Exemple réel obtenu avec une campagne interrompue
après la mise à jour de TF1 et France 2 :

~~~text
  14599 TF1
   8390 France 2
~~~

Nombre total : 22989 émissions.

### Titres et sous-titres d'une chaîne

~~~bash
jq -r '.programs[]
  | select(.channel == "TF1")
  | [.title, .subtitle]
  | @tsv' captvty-cache-28.json
~~~

### Recherche dans les titres et sous-titres

~~~bash
jq -r '.programs[]
  | select(
      ((.title // "") | test("plus belle la vie"; "i")) or
      ((.subtitle // "") | test("plus belle la vie"; "i"))
    )
  | [.channel, .title, .subtitle]
  | @tsv' captvty-cache-28.json
~~~

### Recherche parmi toutes les chaînes

~~~bash
jq -r '.programs[]
  | select(
      ((.title // "") + " " + (.subtitle // ""))
      | test("Saint-Pierre"; "i")
    )
  | [.channel, .title, .subtitle]
  | @tsv' captvty-cache-28.json
~~~

L'option i de test rend la recherche insensible à la casse,
mais pas nécessairement aux accents.

### État de la dernière mise à jour

~~~bash
jq -r '.status
  | to_entries[]
  | [.key, .value]
  | @tsv' captvty-cache-28.json
~~~

### Chaînes dont la mise à jour a échoué

~~~bash
jq -r '.status
  | to_entries[]
  | select(.value != "ok")
  | [.key, .value]
  | @tsv' captvty-cache-28.json
~~~

Cette commande affiche également les catalogues vides
et les interrogations ayant dépassé le timeout.

### Affichage en colonnes

Si la commande column est disponible, les sorties TSV
peuvent être alignées :

~~~bash
jq -r '.status
  | to_entries[]
  | [.key, .value]
  | @tsv' captvty-cache-28.json | column -t -s $'\t'
~~~

### Progression d'une campagne complète

Les commandes permettant de consulter les chaînes terminées,
les chaînes restantes et la prochaine chaîne à traiter
sont décrites dans la section consacrée à la structure
du fichier captvty-cache-28.json.

### Exploitation du champ episode_id

Le champ episode_id contient la valeur brute retournée
par le provider.

Il peut contenir un numéro de saison et d'épisode suivi
du sous-titre, un simple texte ou une valeur vide.

#### Statistiques des identifiants

~~~bash
jq '{
    total: (.programs | length),
    vides: ([.programs[] |
        select((.episode_id // "") == "")] | length),
    numerotes: ([.programs[] |
        select((.episode_id // "") |
        test("^S[0-9]+E[0-9]+\\b"; "i"))] | length),
    autres: ([.programs[] |
        select((.episode_id // "") != "") |
        select((.episode_id // "") |
        test("^S[0-9]+E[0-9]+\\b"; "i") | not)] | length)
}' captvty-cache-28.json
~~~

Résultat réel obtenu en novembre 2026 avec un cache
contenant France 2, France 3 et T18 :

~~~json
{
  "total": 22062,
  "vides": 396,
  "numerotes": 1945,
  "autres": 19721
}
~~~

Seulement 8,82 % des entrées possèdent un identifiant
commençant par un numéro de saison et d'épisode reconnu.

#### Recherche des épisodes d'une saison

Exemple : saison 1 de Saint-Pierre sur France 3.

~~~bash
jq -r '.programs[]
    | select(.channel == "France 3")
    | select(.title == "Saint-Pierre")
    | select((.episode_id // "") |
        test("^S0*1E[0-9]+\\b"; "i"))
    | [.title, .subtitle, .episode_id]
    | @tsv' captvty-cache-28.json
~~~

#### Recherche d'un épisode précis

Exemple : saison 1, épisode 4.

~~~bash
jq -r '.programs[]
    | select(.channel == "France 3")
    | select(.title == "Saint-Pierre")
    | select((.episode_id // "") |
        test("^S0*1E0*4\\b"; "i"))
    | [.title, .subtitle, .episode_id]
    | @tsv' captvty-cache-28.json
~~~

La limite de mot empêche notamment de confondre
l'épisode 4 avec les épisodes 40, 41, etc.

#### Tri numérique par saison et épisode

~~~bash
jq -r '
    [.programs[]
    | select(.channel == "France 3")
    | select(.title == "Saint-Pierre")
    | select((.episode_id // "") |
        test("^S[0-9]+E[0-9]+\\b"; "i"))
    | . + (
        .episode_id |
        capture("^S(?<saison>[0-9]+)E(?<episode>[0-9]+)"; "i")
      )
    | .saison |= tonumber
    | .episode |= tonumber]
    | sort_by(.saison, .episode)
    | .[]
    | [.saison, .episode, .title, .subtitle]
    | @tsv
' captvty-cache-28.json
~~~

Résultat validé :

~~~text
1  1   Saint-Pierre  Le miel et les affaires
1  2   Saint-Pierre  Fête nationale
1  3   Saint-Pierre  Le couperet est tombé
1  4   Saint-Pierre  L'île des évadés
1  5   Saint-Pierre  L'homme des tourbières
1  6   Saint-Pierre  Gallagher & fils
1  7   Saint-Pierre  Un fantôme du passé
1  8   Saint-Pierre  Le corps manquant
1  9   Saint-Pierre  Mauvais amis, bons ennemis
1  10  Saint-Pierre  Qui sème le vent...
~~~

La fonction capture extrait les numéros de saison
et d'épisode.

La fonction tonumber les convertit en entiers,
ce qui permet un véritable tri numérique.

Contrairement à la commande list de Captvty CLI,
cette requête exclut les entrées non numérotées.

Toutes ces requêtes consultent le cache sans le modifier.

### Diagnostic des protections DRM

Les investigations historiques concernant les flux M6+,
les métadonnées JSON et les protections DRM sont décrites
dans [docs/M6-DRM-HISTORIQUE.md](docs/M6-DRM-HISTORIQUE.md).

Le moteur instrumenté utilisé lors de ces investigations
n'a pas été revalidé avec Captvty 3.0.1.28.

## Inspection de la structure JSON du cache

### Clés de premier niveau

~~~bash
jq 'keys' captvty-cache-28.json
~~~

### Types des éléments de premier niveau

~~~bash
jq 'to_entries[] | "\(.key): \(.value | type)"' \
    captvty-cache-28.json
~~~

### Structure d'une émission

~~~bash
jq '.programs[0]' captvty-cache-28.json
~~~

Afficher uniquement les clés :

~~~bash
jq '.programs[0] | keys' captvty-cache-28.json
~~~

Les entrées du cache de la version 2.9.0 contiennent
les champs suivants :

~~~json
[
  "channel",
  "episode_id",
  "subtitle",
  "title"
]
~~~

### État des chaînes

~~~bash
jq '.status' captvty-cache-28.json
~~~

### Structure récursive du cache

La fonction suivante permet de visualiser la structure
JSON en remplaçant les valeurs par leurs types.

~~~bash
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
' captvty-cache-28.json
~~~

Pour les tableaux, seul le premier élément est analysé.
Cette commande fournit donc un aperçu de la structure,
et non un schéma exhaustif de toutes les entrées.

Le champ update_session peut être présent lorsqu'une
campagne complète de mise à jour a été enregistrée.

### Documentation historique

Les anciens exemples utilisant captvty-cache.json
sont conservés dans
[docs/CACHE-HISTORIQUE.md](docs/CACHE-HISTORIQUE.md).

## Vérification des fichiers téléchargés

Un téléchargement terminé avec succès ne garantit pas
que le fichier vidéo soit intégralement lisible.

### Identification des flux

~~~bash
ffprobe -v error \
    -show_entries stream=index,codec_type,codec_name,codec_tag_string,codec_tag,width,height:stream_tags=language \
    -of compact \
    "Vidéos/fichier.ts"
~~~

Cette commande permet notamment de vérifier :

- le codec vidéo ;
- la résolution ;
- les pistes audio ;
- les langues des pistes audio, lorsqu'elles sont renseignées.

### Durée, taille et débit

~~~bash
ffprobe -v error \
    -show_entries format=duration,size,bit_rate \
    -of default=noprint_wrappers=1 \
    "Vidéos/fichier.ts"
~~~

Ces commandes fonctionnent également avec les fichiers MP4
téléchargés depuis T18.

### Limites de la vérification

ffprobe permet d'examiner la structure et les métadonnées
du fichier, mais ne garantit pas son décodage intégral.

Un fichier MPEG-TS structurellement valide peut encore
contenir des flux protégés par DRM.

Les observations historiques concernant M6, W9 et 6ter
sont conservées dans
[docs/M6-DRM-HISTORIQUE.md](docs/M6-DRM-HISTORIQUE.md).

## Interface interactive TUI

Captvty CLI dispose également d'une interface interactive
en mode texte, développée en Python avec curses.

### Prérequis

- Python 3 avec le module curses ;
- un terminal compatible ;
- Captvty CLI installé ;
- un catalogue captvty-cache-28.json disponible.

Aucune bibliothèque Python supplémentaire n'est nécessaire
pour la TUI elle-même.

### Lancement

Depuis le répertoire d'installation de Captvty :

~~~bash
python3 captvty-tui.py
~~~

Si le cache n'existe pas encore, effectuer préalablement
une mise à jour du catalogue :

~~~bash
mono captvty-cli.exe update
~~~

Ou mettre à jour uniquement les chaînes souhaitées :

~~~bash
mono captvty-cli.exe update "France 2" "France 3" "T18"
~~~

### Organisation de l'interface

L'interface comporte deux panneaux :

- à gauche, la liste des chaînes ;
- à droite, la liste des émissions.

La TUI charge le catalogue local au démarrage.

La navigation et la recherche dans le catalogue
ne nécessitent pas d'interrogation réseau.

### Raccourcis clavier

| Touche | Action |
|---|---|
| Flèche haut | Sélection précédente |
| Flèche bas | Sélection suivante |
| Flèches gauche/droite | Changer de panneau |
| Tab | Changer de panneau |
| / | Rechercher dans le catalogue |
| Entrée | Accéder aux émissions ou afficher le détail |
| I | Interroger les informations détaillées |
| Q | Quitter |

### Recherche dans le catalogue

La touche / permet de rechercher des émissions
dans l'ensemble du catalogue.

La recherche est insensible à la casse et aux accents.

La ponctuation est normalisée et tous les mots recherchés
doivent être présents dans les informations de l'émission.

La recherche porte sur :

- le nom de la chaîne ;
- le titre ;
- le numéro de saison et d'épisode reconnu ;
- le sous-titre.

Exemples :

| Recherche | Résultat recherché |
|---|---|
| Saint-Pierre | Émissions correspondant à Saint-Pierre |
| Saint-Pierre S01 | Épisodes de la saison 1 |
| Saint-Pierre S01E04 | Saison 1, épisode 4 |
| France 3 Saint-Pierre | Recherche limitée par le nom de chaîne |
| The Closer | Émissions correspondant à The Closer |
| T18 The Closer | Émissions T18 correspondant à The Closer |

La recherche par nom de chaîne est une recherche textuelle,
et non un filtre strict équivalent à l'option CLI --channel.

### Tri des émissions

La TUI extrait les numéros de saison et d'épisode
depuis le début du champ episode_id.

Les émissions de chaque chaîne sont triées selon :

1. le titre, indépendamment de la casse ;
2. la présence d'un numéro de saison et d'épisode ;
3. le numéro de saison ;
4. le numéro d'épisode ;
5. le sous-titre, indépendamment de la casse.

Les numéros de saison et d'épisode sont comparés
numériquement et non alphabétiquement.

Pour un même titre, les épisodes numérotés sont donc
affichés avant les entrées non numérotées.

Exemple :

~~~text
S01E01  Saint-Pierre - Le miel et les affaires
S01E02  Saint-Pierre - Fête nationale
S01E03  Saint-Pierre - Le couperet est tombé
S01E04  Saint-Pierre - L'île des évadés
S01E05  Saint-Pierre - L'homme des tourbières
S01E06  Saint-Pierre - Gallagher & fils
S01E07  Saint-Pierre - Un fantôme du passé
S01E08  Saint-Pierre - Le corps manquant
S01E09  Saint-Pierre - Mauvais amis, bons ennemis
S01E10  Saint-Pierre - Qui sème le vent...
~~~

### Consultation d'une émission

La touche Entrée affiche les informations locales :

- chaîne ;
- titre ;
- numéro d'épisode, lorsqu'il est reconnu ;
- sous-titre.

Cette consultation utilise uniquement le cache.

### Informations détaillées et téléchargement

La touche I déclenche une interrogation réelle
avec la commande info de Captvty CLI.

Lorsque des qualités vidéo sont détectées dans la réponse,
une fenêtre de sélection des qualités est affichée.

| Touche | Action |
|---|---|
| Flèche haut | Qualité précédente |
| Flèche bas | Qualité suivante |
| G | Télécharger avec la qualité sélectionnée |
| Échap | Annuler |
| Q | Annuler |
| Entrée | Fermer sans télécharger |

Le téléchargement est effectué par la commande get
de Captvty CLI.

La TUI affiche ensuite le résultat de cette commande.

Les interrogations info et get nécessitent un accès réseau.

### Limites

La TUI exploite le catalogue local et ne le met pas
automatiquement à jour.

La sélection des qualités repose sur les informations
retournées par la CLI.

Les commandes spécifiques aux sous-titres TV5MONDE+
restent documentées dans la section CLI correspondante.

### Validation

Les fonctionnalités de recherche, de normalisation
des accents et de tri numérique des épisodes ont été
validées avec Captvty CLI 2.9.0.

Huit tests automatisés de la TUI ont également été exécutés
avec succès.

## Tests automatisés

Le répertoire tests contient les outils de validation
de Captvty CLI et de son interface TUI.

| Fichier | Rôle |
|---|---|
| tests/test-cli.sh | Tests automatisés de la CLI |
| tests/test-tui.py | Tests unitaires de la TUI |
| tests/fake-audio-provider.cs | Provider simulé pour les tests audio |

### Tests CLI hors réseau

Depuis le répertoire du dépôt :

~~~bash
bash tests/test-cli.sh
~~~

Par défaut, le script utilise ./captvty-cli.exe.

Pour tester une installation située dans un autre répertoire :

~~~bash
CLI="/chemin/vers/captvty-cli.exe" bash tests/test-cli.sh
~~~

Les variables disponibles sont :

| Variable | Valeur par défaut | Rôle |
|---|---|---|
| CLI | ./captvty-cli.exe | Exécutable CLI à tester |
| MONO | mono | Exécutable Mono |
| TEST_NETWORK | 0 | Activation des tests réseau avec 1 |

Les tests hors réseau couvrent notamment :

- les arguments manquants ou incorrects ;
- les commandes inconnues ;
- l'affichage de la version ;
- la validation des timeouts ;
- l'option --channel ;
- le regroupement des résultats de recherche ;
- la recherche dans un cache sans moteur Captvty ;
- la détection des dépendances CefSharp manquantes ;
- le calcul de la taille estimée avec les pistes audio.

Les tests utilisant des catalogues ou des providers
simulés sont exécutés dans des répertoires temporaires.

Résultat obtenu avec la version 2.9.0 :

~~~text
PASS: 23
FAIL: 0
~~~

### Tests CLI avec accès réseau

~~~bash
TEST_NETWORK=1 CLI="/chemin/vers/captvty-cli.exe" \
    bash tests/test-cli.sh
~~~

Cette commande exécute les tests hors réseau ainsi que
cinq tests supplémentaires concernant T18.

Les tests T18 couvrent :

1. la mise à jour réelle du catalogue ;
2. la consultation de la liste des épisodes ;
3. la récupération des informations et qualités vidéo ;
4. la gestion d'un échec simulé de résolution ;
5. la gestion d'un échec simulé de récupération.

Le replay utilisé est sélectionné dynamiquement dans
le catalogue obtenu lors de la mise à jour.

Les tests ne dépendent donc plus d'un titre ou d'un épisode
prédéfini susceptible de disparaître du catalogue.

Si la mise à jour échoue, les tests suivants ne sont pas
exécutés à partir d'un ancien catalogue.

Les simulations d'erreurs utilisent les variables :

- CAPTVTY_TEST_T18_RESOLVE_FAIL ;
- CAPTVTY_TEST_T18_FETCH_FAIL.

Ces variables sont destinées exclusivement aux tests.

Attention : les tests réseau actualisent le catalogue T18
de l'installation testée. Il est préférable d'utiliser
une installation dédiée aux tests.

Les tests réseau ne réalisent pas de téléchargement
complet d'une vidéo.

Résultat obtenu avec la version 2.9.0 :

~~~text
PASS: 28
FAIL: 0
~~~

Temps d'exécution observé : environ 26 secondes.

### Tests automatisés de la TUI

~~~bash
python3 tests/test-tui.py
~~~

Les tests utilisent le module unittest de Python.

Les huit tests couvrent :

| Test | Fonctionnalité |
|---|---|
| test_load_cache | Chargement du catalogue |
| test_episode_extraction | Extraction des numéros d'épisode |
| test_episode_numeric_sort | Tri numérique |
| test_without_episode | Entrées sans numéro d'épisode |
| test_search_all_episodes | Recherche des épisodes |
| test_search_normalization | Normalisation des textes |
| test_search_channel | Recherche par nom de chaîne |
| test_search_no_result | Recherche sans résultat |

Ces tests ne nécessitent ni accès réseau ni interaction
avec un terminal curses.

Résultat obtenu :

~~~text
Ran 8 tests

OK
~~~

### Bilan de validation de la version 2.9.0

| Catégorie | Réussis | Échecs |
|---|---:|---:|
| CLI hors réseau | 23 | 0 |
| CLI réseau supplémentaires | 5 | 0 |
| TUI | 8 | 0 |
| Total | 36 | 0 |

Les tests ont été exécutés sous Debian 13.

Le script de tests CLI retourne 0 si tous les tests
exécutés réussissent, et 1 en cas d'échec.

Les résultats réseau peuvent évoluer en fonction
des modifications des plateformes de replay.

Ces tests ne constituent pas une validation exhaustive
des 54 chaînes ni de tous les formats de téléchargement.

Les téléchargements réels et les vérifications ffprobe
ont fait l'objet de tests manuels complémentaires.

## Codes de retour

Captvty CLI utilise ses propres codes de retour et peut
également propager ceux du provider Captvty.

### Tableau des principaux codes

| Code | Signification |
|---:|---|
| 0 | Commande terminée normalement |
| 1 | Erreur générale ou timeout de la CLI |
| 2 | Arguments incorrects, commande inconnue, dépendance requise absente ou aucune campagne à reprendre |
| 3 | Cache absent ou vide, chaîne introuvable, émission ou épisode introuvable, sous-titres indisponibles, sélection T18 ambiguë |
| 4 | Aucune émission correspondante pour un téléchargement ou aucun média téléchargeable |
| 5 | Moteur de téléchargement Captvty introuvable |
| 6 | Constructeur interne de téléchargement introuvable |
| 7 | Échec du téléchargement ou de la récupération/résolution T18 |
| 8 | Délai maximal de téléchargement dépassé |
| 9 | Recherche ambiguë dans le provider Captvty |

La signification exacte dépend de la commande exécutée
et du message affiché.

### Particularités de T18

Le code 7 peut notamment correspondre à :

- un échec de récupération du catalogue ;
- un échec de résolution après deux essais ;
- un média ou une qualité indisponible au second essai ;
- un second téléchargement en échec ;
- l'absence de chemin du fichier final retourné par yt-dlp.

Le code 8 indique un dépassement du délai maximal
pendant le téléchargement T18.

### Particularités de TV5MONDE+

Le code 3 peut notamment correspondre à :

- une série introuvable ;
- un épisode introuvable ;
- des sous-titres indisponibles ;
- aucun fichier de sous-titres téléchargé.

### Particularités du provider Captvty

Les codes 5 et 6 concernent les composants internes
nécessaires au téléchargement.

Le code 7 correspond notamment aux états internes
de téléchargement 2 et 9.

Le code 8 est retourné lorsque le téléchargement
est interrompu après 12 heures par le provider.

Le code 9 indique que plusieurs émissions correspondent
à la recherche effectuée par le provider.

### Particularités de la commande update

La commande update peut retourner 0 même si certaines
chaînes n'ont pas été actualisées correctement.

Le résultat individuel de chaque chaîne doit être
consulté dans le champ status du cache.

Les valeurs possibles sont :

| État | Signification |
|---|---|
| ok | Catalogue récupéré |
| empty | Catalogue vide |
| timeout | Délai maximal dépassé |
| error | Erreur de récupération |

~~~bash
jq -r '.status
  | to_entries[]
  | select(.value != "ok")
  | [.key, .value]
  | @tsv' captvty-cache-28.json
~~~

### Utilisation dans un script shell

~~~bash
mono captvty-cli.exe info "France 3" "Saint-Pierre"
rc=$?

echo "Code de retour : $rc"
~~~

Un code 0 ne garantit pas nécessairement la présence
de résultats pour une recherche.

Pour une campagne de mise à jour, il ne garantit pas
non plus le succès de toutes les chaînes.

Certains codes retournés par le provider peuvent être
propagés directement par la CLI.

## Version 2.6.0

La version 2.6.0 apporte principalement :

- une installation simplifiée avec `install.sh`, avec vérification de
  l'installation Captvty, compilation automatique, sauvegarde de
  l'installation précédente et installation des fichiers générés ;
- l'option globale `--timeout`, avec des délais par défaut adaptés aux
  différentes opérations et `--timeout 0` pour désactiver la limite ;
- l'affichage de la durée des émissions dans les résultats de `info` ;
- une estimation plus réaliste de la taille des téléchargements, tenant
  compte des pistes audio supplémentaires présentes dans les playlists
  HLS et de la marge utilisée par Captvty ; ces tailles sont indiquées
  comme `(estimé)` ;
- la sélection de qualité pour `get` avec `high`, `low` ou le numéro
  d'une qualité particulière ;
- une interface terminal avec `captvty-tui.py` ;
- l'option `--version` pour afficher la version installée.

La version 2.6.0 conserve les correctifs de compatibilité Mono ainsi que
les patchs spécifiques nécessaires notamment pour BFMTV et M6.

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

### Captvty original 3.0.1.28

Empreinte du fichier Captvty.exe original attendu
par le script install.sh :

~~~text
13e04a10367e2e7c7492104a81d110470d8def2cd965037d69d2d5d634f756a7  Captvty.exe
~~~

### Sources de la version 2.9.0

~~~text
1188359b034c94d537084decd42da3a6e0f18148480b10b441e56641b6686726  captvty-cli-v2.9.0.cs
07a1f06c196822c581ff3947d2bc808deca4567613d8acecda15c1ac0a852a66  captvty-provider-28.cs
1962aca570cb0ad1aa6ddc2b8f1110565995c7c847f40eef9b1c91b17e83b418  patch-dpi-28.cs
972c1187736469acc30e5d9d6d0d76c7c8fc35c14ceafad7858d8bf247bfca8a  patch-28-progress.cs
84a250aacea0468582cbb17b7d5971822f49bf8fd4884d1f20364a606287bbea  captvty-tui.py
e2372a0742b95846db5b2853a9743cda29cd93d2d01b119b1b5cffadd6aa5e5d  install.sh
~~~

### Sources des tests

~~~text
8e4d6707f3f350c3deb331fca43b9cd65ccf2d3c15ebae84f69c057aa1e217d4  tests/test-cli.sh
ca7d86057eb5e84ac0e6156d1415bd304d12556f6b4b333c7e5434dab1c00816  tests/test-tui.py
79eb0efa802cfe10ffaee7ee943743f68c802dba68fc3b413c4df2db2067e27c  tests/fake-audio-provider.cs
~~~

### Vérification des empreintes

Depuis le répertoire du dépôt :

~~~bash
sha256sum captvty-cli-v2.9.0.cs \
    captvty-provider-28.cs \
    patch-dpi-28.cs \
    patch-28-progress.cs \
    captvty-tui.py install.sh
~~~

Les empreintes concernent les fichiers sources.

Les exécutables produits lors de la compilation
ne sont pas nécessairement identiques octet pour octet
entre deux constructions.

### Empreintes historiques

Source `captvty-cli-v2.5.0.cs` :

```text
sha256sum captvty-cli-v2.5.0.cs
596b862f0351b442156c57d0139a1528a280f45b22c1c4bea857a99f77d41f46  captvty-cli-v2.5.0.cs
```

## Remarque

Ce projet repose sur l'observation et l'appel d'éléments internes de Captvty.

Les noms internes pouvant changer entre versions de Captvty, une nouvelle version de Captvty peut nécessiter une adaptation du moteur, des patches ou du worker.

Le dépôt ne fournit pas les binaires de Captvty lui-même.
