# Historique des mises à jour du catalogue

Résultats antérieurs à Captvty CLI 2.9.0.
Ces résultats ne constituent pas une validation de la 2.9.0.

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
[19/54] T18 ... OK (...)
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
