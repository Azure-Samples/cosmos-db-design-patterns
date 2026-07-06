namespace Cosmos.VectorSearch;

/// <summary>
/// A small, <b>fictional</b> movie catalog (hand-written for this sample &mdash; these are not real
/// films) used to seed the demo. The plots are deliberately varied across genres so that semantic
/// (meaning-based) search results are easy to see &mdash; a query like "space battle with aliens"
/// should surface the sci-fi films even though it shares no keywords with their titles.
/// </summary>
public static class MovieCatalog
{
    public static IReadOnlyList<Movie> All { get; } = new List<Movie>
    {
        new() { Id = "m01", Title = "Starfall Odyssey", Year = 2016, Genre = "Sci-Fi", Plot = "A ragtag crew aboard a damaged starship fights off an alien armada while racing to warn Earth of an impending invasion." },
        new() { Id = "m02", Title = "The Last Colony", Year = 2019, Genre = "Sci-Fi", Plot = "Settlers on a distant planet must survive hostile extraterrestrial creatures after their supply ships stop arriving." },
        new() { Id = "m03", Title = "Quantum Drift", Year = 2021, Genre = "Sci-Fi", Plot = "A physicist discovers how to jump between parallel universes and must stop a version of herself from collapsing them all." },
        new() { Id = "m04", Title = "Silent Precinct", Year = 2014, Genre = "Crime", Plot = "A weary detective chases a methodical serial killer who leaves cryptic puzzles at every crime scene." },
        new() { Id = "m05", Title = "The Long Con", Year = 2018, Genre = "Crime", Plot = "A team of thieves plans an elaborate casino heist, but betrayal turns the perfect robbery into chaos." },
        new() { Id = "m06", Title = "Cold Evidence", Year = 2012, Genre = "Crime", Plot = "Two homicide investigators reopen a decades-old cold case and uncover corruption reaching the top of the city." },
        new() { Id = "m07", Title = "Paris in the Rain", Year = 2017, Genre = "Romance", Plot = "Two strangers keep crossing paths in a rainy city and slowly fall in love over a single unforgettable week." },
        new() { Id = "m08", Title = "Letters to You", Year = 2015, Genre = "Romance", Plot = "A widow rediscovers hope when she begins exchanging heartfelt letters with a stranger from another town." },
        new() { Id = "m09", Title = "The Wedding Detour", Year = 2020, Genre = "Romance", Plot = "A cynical planner and a hopeless romantic bicker their way across the country to save a friend's wedding." },
        new() { Id = "m10", Title = "Office Havoc", Year = 2013, Genre = "Comedy", Plot = "A hapless intern accidentally becomes CEO for a day and turns the entire corporation upside down." },
        new() { Id = "m11", Title = "Grandma's Getaway", Year = 2019, Genre = "Comedy", Plot = "Three grandmothers escape their retirement home for one last wild road trip full of mishaps and laughter." },
        new() { Id = "m12", Title = "The Heist That Wasn't", Year = 2016, Genre = "Comedy", Plot = "A hilariously incompetent gang of robbers keeps foiling their own bank heist in increasingly absurd ways." },
        new() { Id = "m13", Title = "Whisper in the Walls", Year = 2011, Genre = "Horror", Plot = "A family moves into an old farmhouse and awakens a vengeful spirit that haunts them through the night." },
        new() { Id = "m14", Title = "The Hollow Woods", Year = 2018, Genre = "Horror", Plot = "Campers are stalked by a monstrous creature lurking deep within a forest that seems to shift around them." },
        new() { Id = "m15", Title = "Nightlight", Year = 2022, Genre = "Horror", Plot = "A young girl insists something in the dark is watching her, and only her skeptical brother begins to believe it." },
        new() { Id = "m16", Title = "The Dragon's Heir", Year = 2015, Genre = "Fantasy", Plot = "A humble blacksmith discovers he is the last heir to a dragon throne and must unite kingdoms against a dark sorcerer." },
        new() { Id = "m17", Title = "Realm of Ember", Year = 2020, Genre = "Fantasy", Plot = "A young mage embarks on a magical journey across enchanted lands to recover a stolen relic of fire." },
        new() { Id = "m18", Title = "The Forgotten Spell", Year = 2013, Genre = "Fantasy", Plot = "Two apprentice witches break an ancient curse that has trapped their village in eternal winter." },
        new() { Id = "m19", Title = "Summit of the Gods", Year = 2017, Genre = "Adventure", Plot = "A daring team of climbers attempts an impossible ascent of a deadly mountain no one has ever survived." },
        new() { Id = "m20", Title = "River of No Return", Year = 2014, Genre = "Adventure", Plot = "Explorers rafting a wild jungle river search for a lost city while dodging rapids, traps, and treasure hunters." },
        new() { Id = "m21", Title = "The Deep Trench", Year = 2021, Genre = "Adventure", Plot = "A submarine crew descends to the ocean floor to recover a sunken vessel and finds far more than they bargained for." },
        new() { Id = "m22", Title = "The Quiet Year", Year = 2016, Genre = "Drama", Plot = "A struggling family in a small town learns to heal after loss over the course of four changing seasons." },
        new() { Id = "m23", Title = "Second Chair", Year = 2019, Genre = "Drama", Plot = "A talented young violinist sacrifices everything to earn a place in a prestigious but ruthless orchestra." },
        new() { Id = "m24", Title = "Paper Boats", Year = 2012, Genre = "Drama", Plot = "A father and his estranged son reconnect during a long summer spent repairing an old fishing boat." },
        new() { Id = "m25", Title = "Pixel Pals", Year = 2018, Genre = "Animation", Plot = "Two mismatched video-game characters escape their arcade cabinet and adventure across a bustling city." },
        new() { Id = "m26", Title = "The Little Comet", Year = 2020, Genre = "Animation", Plot = "A tiny comet dreams of lighting up the night sky and befriends the stars on its journey around the sun." },
        new() { Id = "m27", Title = "Terminal Velocity", Year = 2015, Genre = "Thriller", Plot = "A pilot uncovers a bomb aboard a transatlantic flight and has only hours to stop it before landing." },
        new() { Id = "m28", Title = "The Sixth Witness", Year = 2017, Genre = "Thriller", Plot = "A journalist protecting a key witness is hunted across the city by assassins who always seem one step ahead." },
        new() { Id = "m29", Title = "Blackout Protocol", Year = 2022, Genre = "Thriller", Plot = "A cybersecurity analyst races to stop hackers from shutting down a city's power grid during a heatwave." },
        new() { Id = "m30", Title = "Beyond the Horizon", Year = 2019, Genre = "Sci-Fi", Plot = "Astronauts on humanity's first interstellar voyage confront isolation and a mystery signal from deep space." },
    };
}
