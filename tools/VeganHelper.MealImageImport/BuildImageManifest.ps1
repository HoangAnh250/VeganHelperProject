$ErrorActionPreference = 'Stop'
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$mealCsv = Join-Path $repositoryRoot 'docs\data\meal-plan-seed\meals.csv'
$manifestPath = Join-Path $repositoryRoot 'docs\data\meal-images\curated.json'

$photos = @(
    @{ Key='oats'; Id=8819347; Slug='oat-porridge-on-ceramic-bowl'; Photographer='Sabine Sch.' }
    @{ Key='pancakes'; Id=7937382; Slug='stack-of-fluffy-pancakes-with-fruits-and-maple-syrup'; Photographer='Nicola Barts' }
    @{ Key='smoothie'; Id=4099238; Slug='photo-of-fruits-and-smoothie-on-a-bowl'; Photographer='Taryn Elliott' }
    @{ Key='pho'; Id=11783315; Slug='a-close-up-shot-of-bowl-of-pho'; Photographer='Connor Scott McManus' }
    @{ Key='vietnamese'; Id=36453173; Slug='traditional-vietnamese-meal-with-tofu-and-vegetables'; Photographer='Nam Nguyen' }
    @{ Key='rice'; Id=34429484; Slug='vegan-rice-bowl-with-tofu-and-vegetables'; Photographer='Laura oliveira' }
    @{ Key='stir-fry'; Id=9218768; Slug='vegetable-dish-and-white-rice-on-a-ceramic-plate'; Photographer='Loren Castillo' }
    @{ Key='curry'; Id=16515998; Slug='rice-with-vegetables'; Photographer='Filipp Romanovski' }
    @{ Key='thai-curry'; Id=15797986; Slug='bowl-with-soup'; Photographer='Eaksit Sangdee' }
    @{ Key='pasta'; Id=34132123; Slug='penne-pasta-with-cherry-tomatoes-and-mushrooms'; Photographer='Anya Dunes' }
    @{ Key='mushroom-pasta'; Id=8917285; Slug='pasta-on-a-plate'; Photographer='Vincenzo Malagoli' }
    @{ Key='salad'; Id=20473746; Slug='bowl-with-vegetarian-salad'; Photographer='Galiya Tenisheva' }
    @{ Key='lentil-soup'; Id=14822890; Slug='soup-in-bowl'; Photographer='Nadin Sh' }
    @{ Key='chili'; Id=6546437; Slug='vegan-chili-bowl'; Photographer='Alesia Kozik' }
    @{ Key='toast'; Id=6327134; Slug='close-up-shot-of-avocado-toasts'; Photographer='Tima Miroshnichenko' }
    @{ Key='egg-breakfast'; Id=20044229; Slug='roasted-bread-scramble-eggs-and-tomato-salad'; Photographer='Nothing Ahead' }
    @{ Key='taco'; Id=27590340; Slug='rice-and-beans-burrito'; Photographer='Jayce' }
    @{ Key='egg-rice'; Id=9980875; Slug='fried-egg-on-fried-rice-with-cucumber-slices'; Photographer='Karen Laårk Boshoff' }
    @{ Key='smoothie-bowl'; Id=37487554; Slug='colorful-and-nutritious-smoothie-bowl-with-fruits'; Photographer='Kevin Watzal' }
    @{ Key='buddha-bowl'; Id=37079456; Slug='vibrant-vegan-buddha-bowl-with-quinoa'; Photographer='Kerim Eveyik' }
    @{ Key='tofu-breakfast'; Id=7128847; Slug='breakfast-on-plate'; Photographer='Total Shape' }
    @{ Key='vegetable-salad'; Id=4039006; Slug='photo-of-salad-on-plate'; Photographer='Mirela Missmg Gi' }
    @{ Key='noodle-soup'; Id=12017148; Slug='close-up-photo-of-soup-in-white-bowl'; Photographer='Alexey Demidov' }
    @{ Key='noodles'; Id=29145753; Slug='asian-stir-fry-noodles-with-vegetables-in-bowl'; Photographer='Jonathan Borba' }
    @{ Key='stew'; Id=6120503; Slug='cooked-food-on-ceramic-bowl'; Photographer='Alesia Kozik' }
    @{ Key='roasted-vegetables'; Id=31254438; Slug='delicious-sweet-potato-and-broccoli-plate'; Photographer='Sylwester Ficek' }
    @{ Key='bake'; Id=6607386; Slug='roasted-vegetables-garnish-with-vegetarian-meatballs-and-herbs-on-plate'; Photographer='Geraud pfeiffer' }
    @{ Key='bean-soup'; Id=28286253; Slug='vegan-mexican-bean-soup'; Photographer='Nic Wood' }
    @{ Key='vegetable-curry'; Id=37098170; Slug='delicious-vegetable-curry-in-white-bowl'; Photographer='Ellisa A.' }
)

function Get-PhotoKey([string]$mealName) {
    $name = $mealName.ToLowerInvariant()
    if ($name -match 'pancake') { return 'pancakes' }
    if ($name -match 'smoothie') { return 'smoothie-bowl' }
    if ($name -match 'toast') {
        if ($name -match '\begg\b') { return 'egg-breakfast' }
        return 'toast'
    }
    if ($name -match 'oats|porridge|pudding|yogurt') {
        if ($name -match '^vietnamese') { return 'vietnamese' }
        return 'oats'
    }
    if ($name -match 'tortilla' -and $name -match 'breakfast|\begg\b') { return 'egg-breakfast' }
    if ($name -match 'taco|burrito|tortilla') { return 'taco' }
    if ($name -match 'pasta') { return 'pasta' }
    if ($name -match 'noodle') {
        if ($name -match '^vietnamese') { return 'pho' }
        if ($name -match '\bsoup\b') { return 'noodle-soup' }
        return 'noodles'
    }
    if ($name -match '\b(omelet|omelette|egg|eggs)\b|scrambled eggs') {
        if ($name -match 'rice|sticky') { return 'egg-rice' }
        if ($name -match 'noodle|soup') { return 'noodle-soup' }
        return 'egg-breakfast'
    }
    if ($name -match 'curry') {
        if ($name -match 'thai') { return 'thai-curry' }
        if ($name -match '^vietnamese') { return 'curry' }
        return 'vegetable-curry'
    }
    if ($name -match 'salad') {
        if ($name -match 'quinoa') { return 'salad' }
        return 'vegetable-salad'
    }
    if ($name -match 'chili') { return 'chili' }
    if ($name -match 'soup') {
        if ($name -match '^vietnamese') { return 'pho' }
        if ($name -match 'bean') { return 'bean-soup' }
        return 'lentil-soup'
    }
    if ($name -match 'stew') { return 'stew' }
    if ($name -match 'scramble|hash|breakfast skillet') {
        if ($name -match '\b(omelet|omelette|egg|eggs)\b') { return 'egg-breakfast' }
        return 'tofu-breakfast'
    }
    if ($name -match 'bake|pie|loaf|patty|patties|hash|skillet|roasted|ratatouille') { return 'bake' }
    if ($name -match '^vietnamese') { return 'vietnamese' }
    if ($name -match 'rice|risotto|sticky|pilaf') { return 'rice' }
    if ($name -match 'noodle') { return 'noodles' }
    if ($name -match 'tofu|tempeh|edamame') { return 'stir-fry' }
    if ($name -match 'lentil|bean|chickpea|quinoa|millet|bulgur|couscous') { return 'buddha-bowl' }
    return 'roasted-vegetables'
}

$photoByKey = @{}
foreach ($photo in $photos) { $photoByKey[$photo.Key] = $photo }
$meals = Import-Csv $mealCsv
$manifest = foreach ($meal in $meals) {
    $photo = $photoByKey[(Get-PhotoKey $meal.Name)]
    [pscustomobject]@{
        mealName = $meal.Name
        photoId = $photo.Id
        sourcePage = "https://www.pexels.com/photo/$($photo.Slug)-$($photo.Id)/"
        photographer = $photo.Photographer
        license = 'Pexels License'
        downloadUrl = "https://images.pexels.com/photos/$($photo.Id)/pexels-photo-$($photo.Id).jpeg?auto=compress&cs=tinysrgb&w=960"
    }
}

if ($manifest.Count -ne 200 -or ($manifest.mealName | Select-Object -Unique).Count -ne $manifest.Count) {
    throw "Expected 200 unique seed meals; generated $($manifest.Count)."
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 $manifestPath
$uniquePhotos = ($manifest.photoId | Select-Object -Unique).Count
Write-Output "Generated $($manifest.Count) meal image assignments using $uniquePhotos Pexels illustrations."
