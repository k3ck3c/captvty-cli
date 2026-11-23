# API T18 — Captvty CLI 2.9.0

## Historique

Le support autonome de T18 a été introduit dans la
version 2.7.0 de Captvty CLI.

En novembre 2026, l'ancienne API :

~~~text
https://t18.fr/api/products/SLUG/programs?p=N
~~~

retournait une erreur HTTP 400.

La version 2.9.0 utilise les identifiants numériques
des programmes et le nouvel endpoint replays.

## 1. Découverte des programmes

La méthode FetchT18Programs commence par récupérer :

~~~text
https://t18.fr/
~~~

Elle recherche les liens absolus et relatifs vers
les programmes et leurs épisodes.

Expression régulière utilisée :

~~~text
href="(?:https://t18\.fr)?/prog/([^"/]+-([0-9]+))(?=/|")
~~~

Le premier groupe contient le slug complet du programme.
Le second contient son identifiant numérique.

Les programmes sont dédupliqués par identifiant numérique
dans un Dictionary<string,string>.

Si aucun programme n'est détecté, une exception est levée.

Lors de nos investigations, 24 programmes distincts
avaient été détectés sur la page d'accueil.

## 2. Détermination du nombre de pages

Pour chaque programme, la CLI récupère :

~~~text
https://t18.fr/prog/SLUG
~~~

Le HTML peut contenir les attributs suivants :

~~~html
data-pagination-url="/api/products/35574/replays?p=%7Bpage%7D"
data-pagination-pages-count="2"
~~~

La CLI utilise l'expression régulière :

~~~text
data-pagination-pages-count="([0-9]+)"
~~~

En l'absence de cet attribut, une seule page est interrogée.

## 3. Récupération des replays

Le nouvel endpoint est :

~~~text
https://t18.fr/api/products/ID/replays?p=N
~~~

La numérotation commence à 1.

Exemple observé pendant les investigations :

| Programme | Page | Résultat |
|---|---:|---|
| 35574 | 1 | 12 replays |
| 35574 | 2 | 3 replays |
| 35574 | 3 | HTTP 400 |
| 23229 | 1 | 1 replay |

Le programme 23229 était un programme unitaire.

La CLI interroge uniquement les pages annoncées
par le HTML du programme.

## 4. En-têtes HTTP

Les requêtes utilisent le User-Agent suivant :

~~~text
Mozilla/5.0 (X11; Linux x86_64; rv:140.0) Gecko/20100101 Firefox/140.0
~~~

Pour les requêtes JSON, les en-têtes supplémentaires sont :

~~~text
X-Requested-With: XMLHttpRequest
Referer: https://t18.fr/prog/SLUG
~~~

Les requêtes sont effectuées par la méthode RunCurl.

## 5. Structure des réponses JSON

Chaque réponse est désérialisée en tableau T18Program.

Principaux champs observés :

| Champ | Description |
|---|---|
| id | Identifiant du replay |
| title | Titre du replay |
| url | URL du replay |
| imageUrl | URL de l'image |
| genre | Genre du programme |
| broadcastedAt | Date de diffusion |
| duration | Durée au format ISO 8601 |
| product.id | Identifiant du programme |
| product.title | Titre du programme |
| product.url | URL du programme |
| product.isUnitary | Indicateur de programme unitaire |

Exemple de durée :

~~~text
P0Y0M0DT1H30M25S
~~~

Cette valeur représente 1 heure, 30 minutes et 25 secondes.

## 6. Construction du cache

La méthode FetchT18Catalog transforme les objets
T18Program en objets CacheItem.

| Champ du cache | Origine |
|---|---|
| channel | T18 |
| title | product.title |
| subtitle | title |
| episode_id | Non renseigné par cette méthode |

Le catalogue est enregistré dans captvty-cache-28.json
lors d'une commande update.

## 7. Architecture

~~~text
Page d'accueil T18
        |
        v
Découverte des programmes
        |
        v
Déduplication par identifiant numérique
        |
        v
Page HTML de chaque programme
        |
        v
Nombre de pages
        |
        v
API /api/products/ID/replays?p=N
        |
        v
Désérialisation T18Program[]
        |
        v
FetchT18Catalog
        |
        v
CacheItem[]
        |
        v
captvty-cache-28.json (update)
~~~

## 8. Environnement yt-dlp

La méthode FindYtDlp recherche yt-dlp dans le répertoire
de l'assembly CLI, sous :

~~~text
.ytdlp-venv/bin/yt-dlp
~~~

Si ce fichier n'existe pas, elle retourne yt-dlp,
qui sera recherché dans le PATH.

Versions installées et validées :

- yt-dlp : 2026.10.26
- curl_cffi : 0.17.0

L'environnement Python est préparé par install.sh.

## 9. Validation

La récupération du catalogue T18 a retourné 207 replays
lors des tests de novembre 2026.

Les commandes update, search, list, info et get
ont été testées.

La recherche directe ciblée sur T18 fonctionne
indépendamment du moteur Captvty.

## 10. Limites et maintenance

La découverte des programmes repose sur les liens
présents dans la page d'accueil.

L'exhaustivité du catalogue T18 n'est donc pas garantie.

Une modification du HTML, des attributs de pagination
ou des endpoints peut nécessiter une nouvelle adaptation.

Les identifiants et les nombres de replays indiqués
dans ce document correspondent aux observations réalisées
en novembre 2026.

## 11. Méthodes du code source

Dans captvty-cli-v2.9.0.cs :

- FetchT18Programs : récupération des programmes et replays ;
- FetchT18Catalog : construction des entrées du cache ;
- FindYtDlp : localisation de yt-dlp ;
- RunYtDlp : exécution de yt-dlp ;
- NormalizeText : normalisation des textes ;
- ContainsText : recherche insensible à la casse et aux accents.
