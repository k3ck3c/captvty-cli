# Documentation historique du cache

Documentation antérieure à Captvty CLI 2.9.0.

## Un peu de documentation sur le fichier captvty-cache.json

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
  "T18": "ok",
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
